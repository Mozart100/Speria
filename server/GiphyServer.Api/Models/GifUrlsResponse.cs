namespace GiphyServer.Api.Models;

/// <summary>Application response model wrapping a collection of GIF URLs.</summary>
public sealed class GifUrlsResponse
{
    /// <summary>The collection of GIF URLs returned by the requested operation.</summary>
    public IReadOnlyList<GifUrlResponse> Gifs { get; init; } = [];
}
