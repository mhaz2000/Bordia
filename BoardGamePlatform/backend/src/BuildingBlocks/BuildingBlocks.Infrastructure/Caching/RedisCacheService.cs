using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace BuildingBlocks.Infrastructure.Caching;

/// <summary>
/// Typed wrapper around <see cref="IDistributedCache"/> providing strongly-typed,
/// JSON-serialized get/set/remove operations. Used for distributed caching across services.
/// </summary>
public class RedisCacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDistributedCache _cache;
    private readonly TimeSpan _defaultAbsoluteExpiration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCacheService"/> class.
    /// </summary>
    public RedisCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Retrieves and deserializes a value from the cache, or null if absent.
    /// </summary>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var bytes = await _cache.GetAsync(key, cancellationToken);
        if (bytes is null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(bytes, SerializerOptions);
    }

    /// <summary>
    /// Serializes and stores a value in the cache with the default expiration.
    /// </summary>
    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        => SetAsync(key, value, _defaultAbsoluteExpiration, cancellationToken);

    /// <summary>
    /// Serializes and stores a value in the cache with an explicit expiration.
    /// </summary>
    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan absoluteExpiration,
        CancellationToken cancellationToken = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);
        await _cache.SetAsync(
            key,
            bytes,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = absoluteExpiration
            },
            cancellationToken);
    }

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        => _cache.RemoveAsync(key, cancellationToken);
}
