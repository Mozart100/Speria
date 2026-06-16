using StackExchange.Redis;
using System.Text.Json;

namespace GiphyServer.Api.Cache;

public sealed class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly IDatabase _db;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer multiplexer, ILogger<RedisCacheService> logger)
    {
        _multiplexer = multiplexer;
        _db = multiplexer.GetDatabase();
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            if (!value.HasValue)
                return default;

            return JsonSerializer.Deserialize<T>(value!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable for key {CacheKey}. Falling back to Giphy.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await _db.StringSetAsync(key, json, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET failed for key {CacheKey}. Cache write skipped.", key);
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            var endpoint = _multiplexer.GetEndPoints()[0];
            var server   = _multiplexer.GetServer(endpoint);
            var keys     = server.Keys(pattern: "giphy:*").ToArray();

            if (keys.Length > 0)
                await _db.KeyDeleteAsync(keys);

            _logger.LogInformation("Cache cleared. Removed {Count} key(s).", keys.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear cache.");
        }
    }
}
