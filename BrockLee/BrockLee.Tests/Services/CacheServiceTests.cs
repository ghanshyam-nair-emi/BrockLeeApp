// Test Type     : Unit
// Validation    : CacheService — get/set/invalidate, hash key determinism
// Command       : dotnet test --filter "FullyQualifiedName~CacheServiceTests"

using BrockLee.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BrockLee.Tests.Services;

public sealed class CacheServiceTests
{
    private static CacheService BuildService()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:MetricsTtlSeconds"] = "10",
                ["Cache:LogsTtlSeconds"] = "5",
                ["Cache:ComputeTtlSeconds"] = "30"
            })
            .Build();
        return new CacheService(cache, config);
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheMiss_CallsFactory()
    {
        var svc = BuildService();
        int factoryCalls = 0;

        var result = await svc.GetOrCreateAsync(
            "test:key",
            TimeSpan.FromMinutes(1),
            () => { factoryCalls++; return Task.FromResult(42); });

        result.Should().Be(42);
        factoryCalls.Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheHit_DoesNotCallFactory()
    {
        var svc = BuildService();
        int factoryCalls = 0;

        // First call — miss
        await svc.GetOrCreateAsync("test:key", TimeSpan.FromMinutes(1),
            () => { factoryCalls++; return Task.FromResult(42); });

        // Second call — hit
        await svc.GetOrCreateAsync("test:key", TimeSpan.FromMinutes(1),
            () => { factoryCalls++; return Task.FromResult(99); });

        factoryCalls.Should().Be(1, "factory should only be called once");
    }

    [Fact]
    public async Task Invalidate_ClearsEntry_NextCallHitsFactory()
    {
        var svc = BuildService();
        int factoryCalls = 0;

        await svc.GetOrCreateAsync("test:key", TimeSpan.FromMinutes(1),
            () => { factoryCalls++; return Task.FromResult(1); });

        svc.Invalidate("test:key");

        await svc.GetOrCreateAsync("test:key", TimeSpan.FromMinutes(1),
            () => { factoryCalls++; return Task.FromResult(2); });

        factoryCalls.Should().Be(2, "cache was invalidated — factory called again");
    }

    [Fact]
    public void HashKey_SamePayload_ProducesSameKey()
    {
        var payload = new { age = 29, wage = 50_000 };
        var key1 = CacheService.HashKey("returns:nps", payload);
        var key2 = CacheService.HashKey("returns:nps", payload);

        key1.Should().Be(key2, "same payload should always produce the same hash key");
    }

    [Fact]
    public void HashKey_DifferentPayload_ProducesDifferentKey()
    {
        var key1 = CacheService.HashKey("returns:nps", new { age = 29 });
        var key2 = CacheService.HashKey("returns:nps", new { age = 30 });

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void HashKey_DifferentPrefix_ProducesDifferentKey()
    {
        var payload = new { age = 29 };
        var key1 = CacheService.HashKey("returns:nps", payload);
        var key2 = CacheService.HashKey("returns:index", payload);

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void Ttls_ReadFromConfiguration()
    {
        var svc = BuildService();

        svc.MetricsTtl.Should().Be(TimeSpan.FromSeconds(10));
        svc.LogsTtl.Should().Be(TimeSpan.FromSeconds(5));
        svc.ComputeTtl.Should().Be(TimeSpan.FromSeconds(30));
    }
}