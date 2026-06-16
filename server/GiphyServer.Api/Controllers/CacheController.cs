using GiphyServer.Api.Cache;
using Microsoft.AspNetCore.Mvc;

namespace GiphyServer.Api.Controllers;

/// <summary>Manages the Redis GIF cache.</summary>
[ApiController]
[Route("api/cache")]
[Produces("application/json")]
[Tags("Cache")]
public sealed class CacheController : ControllerBase
{
    private readonly ICacheService _cache;
    private readonly ILogger<CacheController> _logger;

    /// <summary>Initialises a new instance of <see cref="CacheController"/>.</summary>
    public CacheController(ICacheService cache, ILogger<CacheController> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>Removes all cached GIF responses from Redis.</summary>
    /// <response code="204">Cache cleared successfully.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cache reset requested via API.");
        await _cache.ClearAsync();
        return NoContent();
    }
}
