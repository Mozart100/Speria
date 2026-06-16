using GiphyServer.Api.Clients;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GiphyServer.Api.HealthChecks;

/// <summary>
/// Verifies connectivity to the Giphy API by calling the trending endpoint.
/// A 3-second timeout prevents slow health responses from stalling Docker health checks.
/// </summary>
public sealed class GiphyHealthCheck : IHealthCheck
{
    private readonly IGiphyClient _giphyClient;
    private readonly ILogger<GiphyHealthCheck> _logger;

    public GiphyHealthCheck(IGiphyClient giphyClient, ILogger<GiphyHealthCheck> logger)
    {
        _giphyClient = giphyClient;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            await _giphyClient.GetTrendingAsync(cts.Token);
            return HealthCheckResult.Healthy("Giphy API is reachable.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Giphy health check failed.");
            return HealthCheckResult.Unhealthy("Giphy API is unreachable.", ex);
        }
    }
}
