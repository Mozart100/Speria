using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace GiphyServer.Api.HealthChecks;

/// <summary>
/// Verifies connectivity to the Redis cache by issuing a PING command.
/// </summary>
public sealed class RedisCacheHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheHealthCheck> _logger;

    public RedisCacheHealthCheck(IConnectionMultiplexer redis, ILogger<RedisCacheHealthCheck> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy("Redis cache is reachable.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis health check failed.");
            return HealthCheckResult.Unhealthy("Redis cache is unreachable.", ex);
        }
    }
}
