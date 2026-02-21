using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BrockLee.Infrastructure;

/// <summary>
/// Thin wrapper around IMemoryCache providing:
///   1. Typed get-or-create with TTL from config
///   2. Deterministic cache key generation from any payload
///      (SHA256 hash of JSON-serialised request)
///
/// Cache TTLs (from appsettings.json "Cache" section):
///   MetricsTtlSeconds  : 10s  — aggregate metrics panel
///   LogsTtlSeconds     :  5s  — user log table
///   ComputeTtlSeconds  : 30s  — computation results
///
/// Scalability note:
///   IMemoryCache is per-process. For multi-instance horizontal scaling,
///   swap this for IDistributedCache (Azure Cache for Redis). The interface
///   here is designed to make that swap a one-file change.
/// </summary>
public sealed class CacheService
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;

    public CacheService(IMemoryCache cache, IConfiguration config)
    {
        _cache = cache;
        _config = config;
    }

    // ── TTLs ──────────────────────────────────────────────────────────────────

    public TimeSpan MetricsTtl => Ttl("MetricsTtlSeconds", 10);
    public TimeSpan LogsTtl => Ttl("LogsTtlSeconds", 5);
    public TimeSpan ComputeTtl => Ttl("ComputeTtlSeconds", 30);

    private TimeSpan Ttl(string key, int defaultSecs)
    {
        var secs = _config.GetValue<int?>($"Cache:{key}") ?? defaultSecs;
        return TimeSpan.FromSeconds(secs);
    }

    // ── Get or Create ─────────────────────────────────────────────────────────

    public async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        TimeSpan ttl,
        Func<Task<T>> factory)
    {
        if (_cache.TryGetValue(cacheKey, out T? cached) && cached is not null)
            return cached;

        var result = await factory();

        _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Size = 1                         // Required when cache has SizeLimit set
        });

        return result;
    }

    public T? Get<T>(string key)
    {
        _cache.TryGetValue(key, out T? value);
        return value;
    }

    public void Set<T>(string key, T value, TimeSpan ttl)
        => _cache.Set(key, value, ttl);

    public void Invalidate(string key)
        => _cache.Remove(key);

    // ── Key Generation ────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a deterministic SHA256-based cache key from any serialisable object.
    /// Same request payload → same key → cache hit.
    /// </summary>
    public static string HashKey(string prefix, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        var hash = Convert.ToHexString(bytes)[..16];   // First 16 hex chars — enough uniqueness
        return $"{prefix}:{hash}";
    }
}