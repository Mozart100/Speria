using GiphyServer.Api.Models;

namespace GiphyServer.Api.Clients;

/// <summary>
/// Abstraction over the Giphy HTTP API.
/// Decouples the rest of the application from transport and Giphy-specific details,
/// making the client swappable and easily mockable in tests.
/// </summary>
public interface IGiphyClient
{
    /// <summary>Fetches currently trending GIFs from Giphy.</summary>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    Task<GiphyResponse> GetTrendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Searches Giphy GIFs matching the given text term.</summary>
    /// <param name="term">The search term to query Giphy with.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    Task<GiphyResponse> SearchAsync(string term, CancellationToken cancellationToken = default);
}
