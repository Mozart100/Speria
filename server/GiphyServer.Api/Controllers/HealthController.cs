using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GiphyServer.Api.Controllers;

/// <summary>Exposes the application health status.</summary>
[ApiController]
[Route("[controller]")]
[Produces("application/json")]
[Tags("Health")]
public sealed class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    /// <summary>Initialises a new instance of <see cref="HealthController"/>.</summary>
    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    /// <summary>Returns the overall health status of the application.</summary>
    /// <remarks>
    /// Returns <c>200 Healthy</c> when all registered checks pass.
    /// Returns <c>503 Unhealthy</c> when any check fails.
    /// Additional checks (Redis, Giphy, database) can be registered via
    /// <c>builder.Services.AddHealthChecks().AddRedis(...)</c> without touching this controller.
    /// </remarks>
    /// <response code="200">Application is healthy.</response>
    /// <response code="503">One or more health checks failed.</response>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(cancellationToken);

        var response = new HealthResponse(
            Status: report.Status.ToString(),
            Checks: report.Entries.ToDictionary(
                e => e.Key,
                e => new CheckResult(e.Value.Status.ToString(), e.Value.Description)));

        return report.Status == HealthStatus.Healthy
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}

/// <summary>Overall health response returned by <c>GET /health</c>.</summary>
/// <param name="Status">Healthy, Degraded, or Unhealthy.</param>
/// <param name="Checks">Individual check results keyed by check name.</param>
public sealed record HealthResponse(
    string Status,
    Dictionary<string, CheckResult> Checks);

/// <summary>Result of a single named health check.</summary>
/// <param name="Status">Healthy, Degraded, or Unhealthy.</param>
/// <param name="Description">Optional description or error message from the check.</param>
public sealed record CheckResult(
    string Status,
    string? Description);
