namespace GiphyServer.Api.Models;

/// <summary>Application response model representing the URL of a single GIF.</summary>
public sealed class GifUrlResponse
{
    /// <summary>The direct URL to the GIF.</summary>
    public string Url { get; init; } = string.Empty;
}
