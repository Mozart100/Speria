using GiphyServer.Api.Models;
using GiphyServer.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GiphyServer.Api.Controllers;

/// <summary>Exposes endpoints for retrieving GIF URLs from the Giphy platform.</summary>
[ApiController]
[Route("api/gifs")]
[Produces("application/json")]
public sealed class GifsController : ControllerBase
{
    private readonly IGifService _gifService;
    private readonly ILogger<GifsController> _logger;

    /// <summary>Initialises a new instance of <see cref="GifsController"/>.</summary>
    public GifsController(IGifService gifService, ILogger<GifsController> logger)
    {
        _gifService = gifService;
        _logger = logger;
    }

    /// <summary>Returns a collection of currently trending GIF URLs.</summary>
    /// <param name="cancellationToken">Propagates notification that the request has been cancelled.</param>
    /// <response code="200">A list of trending GIF URLs.</response>
    /// <response code="401">Giphy API key is invalid or revoked.</response>
    /// <response code="429">Giphy API rate limit has been exceeded.</response>
    /// <response code="500">An unexpected internal error occurred.</response>
    /// <response code="502">The upstream Giphy API is unavailable.</response>
    /// <response code="503">The application or one of its dependencies is unhealthy.</response>
    [HttpGet("trending")]
    [ProducesResponseType(typeof(GifUrlsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GifUrlsResponse>> GetTrending(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received request for trending GIFs");

        try
        {
            var result = await _gifService.GetTrendingAsync(cancellationToken);
            _logger.LogInformation("Returning {GifCount} trending GIFs", result.Gifs.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trending GIFs request failed");
            throw;
        }
    }

    /// <summary>Searches Giphy for GIFs matching the given term and returns their URLs.</summary>
    /// <param name="term">The text term to search for.</param>
    /// <param name="cancellationToken">Propagates notification that the request has been cancelled.</param>
    /// <response code="200">A list of GIF URLs matching the search term.</response>
    /// <response code="400">The search term was not provided or is empty.</response>
    /// <response code="401">Giphy API key is invalid or revoked.</response>
    /// <response code="429">Giphy API rate limit has been exceeded.</response>
    /// <response code="500">An unexpected internal error occurred.</response>
    /// <response code="502">The upstream Giphy API is unavailable.</response>
    /// <response code="503">The application or one of its dependencies is unhealthy.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(GifUrlsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GifUrlsResponse>> Search(
        [FromQuery] string term,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(term))
            return BadRequest("The 'term' query parameter is required and cannot be empty.");

        _logger.LogInformation("Received Giphy search request {SearchTerm}", term);

        try
        {
            var result = await _gifService.SearchAsync(term, cancellationToken);
            _logger.LogInformation("Returning {GifCount} GIFs for search term {SearchTerm}", result.Gifs.Count, term);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for term {SearchTerm}", term);
            throw;
        }
    }
}
