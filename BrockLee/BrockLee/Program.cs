using BrockLee.Data;
using BrockLee.Hubs;
using BrockLee.Infrastructure;
using BrockLee.Messaging.Consumers;
using BrockLee.Middleware;
using BrockLee.Models.Entities;
using BrockLee.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);

// ── Database — Azure SQL everywhere (dev + prod) ──────────────────────────────
// No SQLite. Use a local Azure SQL instance, Azure SQL free tier,
// or SQL Server LocalDB for development.
// Connection string set via:
//   Dev  → appsettings.Development.json  OR  environment variable
//   Prod → environment variable (never committed)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found. " +
        "Set it in appsettings.Development.json or via environment variable.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
        sql.CommandTimeout(30);
    }));

// ── Cache ─────────────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();

// ── Log write channel (fire-and-forget DB writes) ─────────────────────────────
var logChannel = Channel.CreateUnbounded<UserLogEntity>(
    new UnboundedChannelOptions { SingleReader = true });
builder.Services.AddSingleton(logChannel);
builder.Services.AddSingleton(logChannel.Writer);
builder.Services.AddSingleton(logChannel.Reader);
builder.Services.AddHostedService<LogBackgroundService>();

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddSingleton<PerformanceTracker>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<FilterService>();
builder.Services.AddScoped<ReturnsService>();
builder.Services.AddScoped<LogService>();
builder.Services.AddScoped<CacheService>();

builder.Services.AddHttpClient<PythonBridgeService>(client =>
    client.BaseAddress = new Uri(
        builder.Configuration["PythonService:BaseUrl"] ?? "http://localhost:8000"))
    .AddStandardResilienceHandler();

// ── MassTransit + RabbitMQ ────────────────────────────────────────────────────
var rabbitConfig = builder.Configuration.GetSection("RabbitMq");
var useRabbitMq = !string.IsNullOrEmpty(rabbitConfig["Host"]);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PredictConsumer>();
    x.AddConsumer<PredictResultConsumer>();
    x.AddConsumer<DashboardEventConsumer>();

    if (useRabbitMq)
    {
        x.UsingRabbitMq((ctx, cfg) =>
        {
            cfg.Host(
                rabbitConfig["Host"],
                ushort.Parse(rabbitConfig["Port"] ?? "5672"),
                "/",
                h =>
                {
                    h.Username(rabbitConfig["Username"] ?? "guest");
                    h.Password(rabbitConfig["Password"] ?? "guest");
                });

            cfg.UseMessageRetry(r => r.Exponential(
                retryLimit: 3,
                minInterval: TimeSpan.FromSeconds(1),
                maxInterval: TimeSpan.FromSeconds(30),
                intervalDelta: TimeSpan.FromSeconds(2)));

            cfg.ReceiveEndpoint("brocklee.predict.request", e =>
            {
                e.PrefetchCount = 5;
                e.ConcurrentMessageLimit = 5;
                e.ConfigureConsumer<PredictConsumer>(ctx);
            });

            cfg.ReceiveEndpoint("brocklee.predict.result", e =>
                e.ConfigureConsumer<PredictResultConsumer>(ctx));

            cfg.ReceiveEndpoint("brocklee.dashboard.event", e =>
                e.ConfigureConsumer<DashboardEventConsumer>(ctx));

            cfg.ConfigureEndpoints(ctx);
        });
    }
    else
    {
        // Dev without RabbitMQ running — in-memory transport
        // Consumers still fire, SignalR still works, nothing breaks
        x.UsingInMemory((ctx, cfg) =>
        {
            cfg.ConcurrentMessageLimit = 10;
            cfg.ConfigureEndpoints(ctx);
        });
    }
});

var app = builder.Build();

// ── Auto-migrate on startup ───────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.MapOpenApi();
app.MapScalarApiReference();
app.UseMiddleware<PerformanceMiddleware>();

var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (Directory.Exists(wwwrootPath))
{
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// ── SignalR ───────────────────────────────────────────────────────────────────
app.MapHub<DashboardHub>("/hubs/dashboard");

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    transport = useRabbitMq ? "rabbitmq" : "in-memory",
    mode = Directory.Exists(wwwrootPath) ? "full-stack" : "api-only",
    db = "azure-sql"
}));

app.Run();