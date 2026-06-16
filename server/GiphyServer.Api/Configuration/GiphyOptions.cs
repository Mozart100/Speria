namespace GiphyServer.Api.Configuration;

/// <summary>Strongly-typed configuration for the Giphy API, bound from the "Giphy" config section.</summary>
public sealed class GiphyOptions
{
    /// <summary>The configuration section name used for binding.</summary>
    public const string SectionName = "Giphy";

    /// <summary>Base URL for the Giphy API (must end with a trailing slash).</summary>
    public string BaseUrl { get; init; } = "https://api.giphy.com/v1/";

    /// <summary>Giphy API key. Supplied via the GIPHY_API_KEY environment variable.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Maximum number of GIFs returned per request (1–50). Default: 20.</summary>
    public int ResultLimit { get; init; } = 20;
}
