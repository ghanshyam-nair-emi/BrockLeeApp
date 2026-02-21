// Test Type     : Integration
// Validation    : LogService reads/writes against in-memory EF Core DbContext,
//                 cache invalidation, metrics aggregation
// Command       : dotnet test --filter "FullyQualifiedName~LogServiceIntegrationTests"

using BrockLee.Infrastructure;
using BrockLee.Models.Entities;
using BrockLee.Services;
using BrockLee.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Threading.Channels;
using Xunit;

namespace BrockLee.Tests.Integration;

public sealed class LogServiceIntegrationTests : IDisposable
{
    private readonly BrockLee.Data.AppDbContext _db;
    private readonly Channel<UserLogEntity> _channel;
    private readonly CacheService _cache;
    private readonly LogService _svc;

    public LogServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _channel = Channel.CreateUnbounded<UserLogEntity>();

        var memCache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:MetricsTtlSeconds"] = "10",
                ["Cache:LogsTtlSeconds"] = "5",
                ["Cache:ComputeTtlSeconds"] = "30"
            })
            .Build();

        _cache = new CacheService(memCache, config);
        _svc = new LogService(_db, _channel.Writer, _cache);
    }

    public void Dispose() => _db.Dispose();

    // ── GetLatestAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLatestAsync_EmptyDb_ReturnsEmptyList()
    {
        var logs = await _svc.GetLatestAsync();
        logs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLatestAsync_ReturnsDescendingByLoggedAt()
    {
        // Seed directly to DB (bypass channel for this test)
        _db.UserLogs.AddRange(
            MakeLog("Alice", new DateTime(2023, 1, 1)),
            MakeLog("Bob", new DateTime(2023, 3, 1)),
            MakeLog("Charlie", new DateTime(2023, 2, 1))
        );
        await _db.SaveChangesAsync();

        var logs = await _svc.GetLatestAsync();

        logs.Should().HaveCount(3);
        logs[0].Name.Should().Be("Bob", "most recent first");
        logs[1].Name.Should().Be("Charlie");
        logs[2].Name.Should().Be("Alice", "oldest last");
    }

    [Fact]
    public async Task GetLatestAsync_RespectsCountLimit()
    {
        for (int i = 0; i < 10; i++)
            _db.UserLogs.Add(MakeLog($"User{i}", DateTime.UtcNow.AddMinutes(-i)));
        await _db.SaveChangesAsync();

        var logs = await _svc.GetLatestAsync(count: 3);
        logs.Should().HaveCount(3);
    }

    // ── GetMetricsAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetMetricsAsync_EmptyDb_ReturnsDefaultMetrics()
    {
        var metrics = await _svc.GetMetricsAsync();

        metrics.TotalSubmissions.Should().Be(0);
        metrics.AvgAge.Should().Be(0);
    }

    [Fact]
    public async Task GetMetricsAsync_CalculatesAveragesCorrectly()
    {
        _db.UserLogs.AddRange(
            MakeLog("Alice", DateTime.UtcNow, age: 25, wage: 50_000),
            MakeLog("Bob", DateTime.UtcNow, age: 35, wage: 70_000)
        );
        await _db.SaveChangesAsync();

        var metrics = await _svc.GetMetricsAsync();

        metrics.TotalSubmissions.Should().Be(2);
        metrics.AvgAge.Should().BeApproximately(30, 0.01);
        metrics.AvgWage.Should().BeApproximately(60_000, 0.01);
    }

    // ── LogAsync (channel write) ──────────────────────────────────────────────

    [Fact]
    public async Task LogAsync_WritesToChannel_NotDirectlyToDb()
    {
        var entry = MakeLog("TestUser", DateTime.UtcNow);

        await _svc.LogAsync(entry);

        // Should be in the channel, not the DB yet
        _db.UserLogs.Should().BeEmpty("channel write is fire-and-forget");
        _channel.Reader.Count.Should().Be(1, "entry should be in the channel");
    }

    [Fact]
    public async Task LogAsync_InvalidatesCache()
    {
        // Prime cache
        await _svc.GetLatestAsync();
        await _svc.GetMetricsAsync();

        // Log new entry — should invalidate
        await _svc.LogAsync(MakeLog("NewUser", DateTime.UtcNow));

        // Cache should be clear — next call hits DB
        // (We can't inspect internal cache directly, but this is validated by
        // confirming no stale data is returned after a second DB seed)
        _db.UserLogs.Add(MakeLog("SeedUser", DateTime.UtcNow.AddMinutes(-1)));
        await _db.SaveChangesAsync();

        var logs = await _svc.GetLatestAsync();
        logs.Should().NotBeEmpty("cache was invalidated so fresh DB data is returned");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static UserLogEntity MakeLog(
        string name, DateTime loggedAt, int age = 29, decimal wage = 50_000) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Age = age,
            Wage = wage,
            AnnualIncome = wage * 12,
            ExpenseCount = 4,
            TotalExpenseAmount = 1725,
            TotalRemanent = 145,
            NpsRealValue = 231.9m,
            IndexRealValue = 1829.5m,
            TaxBenefit = 0,
            YearsToRetirement = 60 - age,
            LoggedAt = loggedAt,
            ResponseTimeMs = 42.5
        };
}