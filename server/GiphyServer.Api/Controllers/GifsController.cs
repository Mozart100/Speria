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
    /// <response code="502">The upstream Giphy API returned an error.</response>
    [HttpGet("trending")]
    [ProducesResponseType(typeof(GifUrlsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<GifUrlsResponse>> GetTrending(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received request for trending GIFs");

        var result = await _gifService.GetTrendingAsync(cancellationToken);

        _logger.LogInformation("Returning {GifCount} trending GIFs", result.Gifs.Count);

        return Ok(result);
    }

    /// <summary>Searches Giphy for GIFs matching the given term and returns their URLs.</summary>
    /// <param name="term">The text term to search for.</param>
    /// <param name="cancellationToken">Propagates notification that the request has been cancelled.</param>
    /// <response code="200">A list of GIF URLs matching the search term.</response>
    /// <response code="400">The search term was not provided or is empty.</response>
    /// <response code="502">The upstream Giphy API returned an error.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(GifUrlsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<GifUrlsResponse>> Search(
        [FromQuery] string term,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(term))
            return BadRequest("The 'term' query parameter is required and cannot be empty.");

        _logger.LogInformation("Received Giphy search request {SearchTerm}", term);

        var result = await _gifService.SearchAsync(term, cancellationToken);

        _logger.LogInformation("Returning {GifCount} GIFs for search term {SearchTerm}", result.Gifs.Count, term);

        return Ok(result);
    }
}
