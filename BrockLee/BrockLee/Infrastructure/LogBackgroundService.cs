using BrockLee.Data;
using BrockLee.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading.Channels;

namespace BrockLee.Infrastructure;

/// <summary>
/// Hosted background service that drains the log write channel
/// and persists entries to Azure SQL in batches.
///
/// Why this exists:
///   Writing to Azure SQL on every API request adds ~20-80ms latency
///   (network round-trip to Azure). By decoupling the write into a
///   background channel, the API response is returned immediately
///   and the DB write happens asynchronously — no user-visible delay.
///
/// Scalability:
///   - Batches up to 50 entries per write cycle (configurable)
///   - Drains every 2 seconds or when batch is full
///   - Single reader — no contention
///   - If the app crashes, at most one batch of in-flight logs is lost
///     (acceptable for a public logbook — not financial ledger data)
/// </summary>
public sealed class LogBackgroundService : BackgroundService
{
    private readonly ChannelReader<UserLogEntity> _reader;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LogBackgroundService> _logger;

    private const int BatchSize = 50;
    private const int DrainIntervalMs = 2_000;

    public LogBackgroundService(
        ChannelReader<UserLogEntity> reader,
        IServiceScopeFactory scopeFactory,
        ILogger<LogBackgroundService> logger)
    {
        _reader = reader;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LogBackgroundService started — draining to Azure SQL.");

        var batch = new List<UserLogEntity>(BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Collect up to BatchSize entries within DrainIntervalMs
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(DrainIntervalMs);

                try
                {
                    await foreach (var entry in _reader.ReadAllAsync(cts.Token))
                    {
                        batch.Add(entry);
                        if (batch.Count >= BatchSize) break;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Drain interval elapsed — flush whatever we have
                }

                if (batch.Count > 0)
                {
                    await PersistBatchAsync(batch, stoppingToken);
                    _logger.LogDebug("Flushed {Count} log entries to Azure SQL.", batch.Count);
                    batch.Clear();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in LogBackgroundService drain loop.");
                await Task.Delay(5_000, stoppingToken); // Back off on error
            }
        }

        // Final flush on graceful shutdown
        if (batch.Count > 0)
            await PersistBatchAsync(batch, CancellationToken.None);
    }

    private async Task PersistBatchAsync(List<UserLogEntity> batch, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserLogs.AddRange(batch);
        await db.SaveChangesAsync(ct);
    }
}