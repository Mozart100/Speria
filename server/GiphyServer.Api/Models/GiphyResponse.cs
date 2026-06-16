using System.Text.Json.Serialization;

namespace GiphyServer.Api.Models;

/// <summary>Top-level envelope returned by the Giphy API for trending and search endpoints.</summary>
public sealed class GiphyResponse
{
    /// <summary>The list of GIF items returned by Giphy.</summary>
    [JsonPropertyName("data")]
    public List<GiphyGifDto> Data { get; init; } = [];
}

/// <summary>Represents a single GIF item from the Giphy API response.</summary>
public sealed class GiphyGifDto
{
    /// <summary>Container for the various image renditions available for this GIF.</summary>
    [JsonPropertyName("images")]
    public GiphyImagesDto Images { get; init; } = new();
}

/// <summary>Holds the available image renditions for a GIF.</summary>
public sealed class GiphyImagesDto
{
    /// <summary>The original, full-quality rendition of the GIF.</summary>
    [JsonPropertyName("original")]
    public GiphyImageDto Original { get; init; } = new();
}

/// <summary>Metadata for a single image rendition.</summary>
public sealed class GiphyImageDto
{
    /// <summary>The direct URL to this rendition.</summary>
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;
}
