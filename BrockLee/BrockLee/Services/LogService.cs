using BrockLee.Data;
using BrockLee.Infrastructure;
using BrockLee.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading.Channels;

namespace BrockLee.Services;

/// <summary>
/// Handles all logbook operations.
///
/// Writes : fire-and-forget via Channel — never blocks the API response
/// Reads  : cached via CacheService — minimises Azure SQL round-trips
/// </summary>
public sealed class LogService
{
    private readonly AppDbContext _db;
    private readonly ChannelWriter<UserLogEntity> _writer;
    private readonly CacheService _cache;

    private const string LogsCacheKey = "logs:latest";
    private const string MetricsCacheKey = "logs:metrics";

    public LogService(
        AppDbContext db,
        ChannelWriter<UserLogEntity> writer,
        CacheService cache)
    {
        _db = db;
        _writer = writer;
        _cache = cache;
    }

    // ── Write (non-blocking) ──────────────────────────────────────────────────

    /// <summary>
    /// Queues a log entry for async write to Azure SQL.
    /// Returns immediately — does not await DB confirmation.
    /// Invalidates the cached log + metrics so next poll gets fresh data.
    /// </summary>
    public async ValueTask LogAsync(UserLogEntity entry)
    {
        await _writer.WriteAsync(entry);

        // Invalidate so next dashboard poll reflects new entry
        _cache.Invalidate(LogsCacheKey);
        _cache.Invalidate(MetricsCacheKey);
    }

    // ── Read : Logs ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the latest N log entries, descending by timestamp.
    /// Result is cached for LogsTtl (5s by default) to avoid
    /// hammering Azure SQL on every Angular poll.
    /// </summary>
    public Task<List<UserLogEntity>> GetLatestAsync(int count = 50)
        => _cache.GetOrCreateAsync(
            LogsCacheKey,
            _cache.LogsTtl,
            () => _db.UserLogs
                      .OrderByDescending(x => x.LoggedAt)
                      .Take(Math.Clamp(count, 1, 200))
                      .AsNoTracking()
                      .ToListAsync());

    // ── Read : Metrics ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns aggregate metrics for the dashboard Metric Update panel.
    /// Cached for MetricsTtl (10s) — cheap to recompute, but no need
    /// to hit Azure SQL on every Angular poll cycle.
    /// </summary>
    public Task<LogMetrics> GetMetricsAsync()
        => _cache.GetOrCreateAsync(
            MetricsCacheKey,
            _cache.MetricsTtl,
            async () =>
            {
                var total = await _db.UserLogs.CountAsync();
                if (total == 0) return new LogMetrics();

                return new LogMetrics
                {
                    TotalSubmissions = total,
                    AvgAge = await _db.UserLogs.AverageAsync(x => (double)x.Age),
                    AvgWage = await _db.UserLogs.AverageAsync(x => (double)x.Wage),
                    AvgNpsRealValue = await _db.UserLogs.AverageAsync(x => (double)x.NpsRealValue),
                    AvgIndexRealValue = await _db.UserLogs.AverageAsync(x => (double)x.IndexRealValue),
                    AvgResponseTimeMs = await _db.UserLogs.AverageAsync(x => x.ResponseTimeMs),
                    LastSubmittedAt = await _db.UserLogs.MaxAsync(x => x.LoggedAt)
                };
            });
}

// ── Metrics DTO ───────────────────────────────────────────────────────────────

public sealed class LogMetrics
{
    public int TotalSubmissions { get; init; }
    public double AvgAge { get; init; }
    public double AvgWage { get; init; }
    public double AvgNpsRealValue { get; init; }
    public double AvgIndexRealValue { get; init; }
    public double AvgResponseTimeMs { get; init; }
    public DateTime LastSubmittedAt { get; init; }
}