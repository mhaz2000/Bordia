using StackExchange.Redis;

namespace BuildingBlocks.Infrastructure.Caching;

/// <summary>
/// Atomic Redis counters for fixed-window rate limiting (INCR + first-hit
/// EXPIRE), backed by the shared <see cref="IConnectionMultiplexer"/>.
/// </summary>
public class RedisCounter
{
    private readonly IConnectionMultiplexer _redis;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCounter"/> class.
    /// </summary>
    public RedisCounter(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    /// <summary>
    /// Increments <paramref name="key"/> (setting <paramref name="ttl"/> on first
    /// hit) and reports the resulting count. Atomic per key - unlike a
    /// get/set cache pair, concurrent senders can't both observe the same count.
    /// </summary>
    public async Task<long> IncrementAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var count = await db.StringIncrementAsync(key);
        if (count == 1)
        {
            await db.KeyExpireAsync(key, ttl);
        }

        return count;
    }
}
