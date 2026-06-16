using GiphyServer.Api.Models;

namespace GiphyServer.Api.Services;

/// <summary>
/// Application-level abstraction for GIF retrieval.
/// This interface is the seam between the controller layer and the provider layer,
/// allowing caching decorators or alternative providers to be introduced without
/// any changes to controllers.
/// </summary>
public interface IGifService
{
    /// <summary>Returns a collection of currently trending GIF URLs.</summary>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    Task<GifUrlsResponse> GetTrendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns GIF URLs matching the given search term.</summary>
    /// <param name="term">The text term to search for.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    Task<GifUrlsResponse> SearchAsync(string term, CancellationToken cancellationToken = default);
}
