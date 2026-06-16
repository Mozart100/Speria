using GiphyServer.Api.Configuration;
using GiphyServer.Api.Models;
using Microsoft.Extensions.Options;

namespace GiphyServer.Api.Clients;

/// <summary>
/// HTTP implementation of <see cref="IGiphyClient"/> backed by a typed <see cref="HttpClient"/>
/// registered via HttpClientFactory. Handles request construction, authentication,
/// and deserialization of Giphy API responses.
/// </summary>
public sealed class GiphyHttpClient : IGiphyClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GiphyHttpClient> _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="GiphyHttpClient"/>.
    /// The <see cref="HttpClient"/> base address is configured during DI registration.
    /// </summary>
    public GiphyHttpClient(HttpClient httpClient, IOptions<GiphyOptions> options, ILogger<GiphyHttpClient> logger)
    {
        _httpClient = httpClient;
        _apiKey = options.Value.ApiKey;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<GiphyResponse> GetTrendingAsync(CancellationToken cancellationToken = default)
    {
        var url = $"gifs/trending?api_key={_apiKey}&limit=20";
        return FetchAsync(url, endpoint: "trending", searchTerm: null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<GiphyResponse> SearchAsync(string term, CancellationToken cancellationToken = default)
    {
        var encodedTerm = Uri.EscapeDataString(term);
        var url = $"gifs/search?api_key={_apiKey}&q={encodedTerm}&limit=20";
        return FetchAsync(url, endpoint: "search", searchTerm: term, cancellationToken);
    }

    private async Task<GiphyResponse> FetchAsync(
        string relativeUrl,
        string endpoint,
        string? searchTerm,
        CancellationToken cancellationToken)
    {
        var safeUrl = SanitizeUrl(relativeUrl);

        _logger.LogInformation(
            "Calling Giphy {GiphyEndpoint} | SearchTerm: {SearchTerm} | Url: {GiphyUrl}",
            endpoint,
            searchTerm ?? "(none)",
            safeUrl);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Giphy request failed for {GiphyEndpoint} | SearchTerm: {SearchTerm} | Error: {ErrorMessage}",
                endpoint,
                searchTerm ?? "(none)",
                ex.Message);
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Giphy {GiphyEndpoint} returned an error | SearchTerm: {SearchTerm} | StatusCode: {StatusCode}",
                endpoint,
                searchTerm ?? "(none)",
                (int)response.StatusCode);

            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<GiphyResponse>(cancellationToken: cancellationToken)
                     ?? new GiphyResponse();

        _logger.LogInformation(
            "Giphy {GiphyEndpoint} succeeded | SearchTerm: {SearchTerm} | StatusCode: {StatusCode} | GifCount: {GifCount}",
            endpoint,
            searchTerm ?? "(none)",
            (int)response.StatusCode,
            result.Data.Count);

        return result;
    }

    // Replaces the api_key value in the URL with *** so it is safe to emit in logs.
    private static string SanitizeUrl(string url)
    {
        var idx = url.IndexOf("api_key=", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return url;

        var valueStart = idx + 8;
        var valueEnd = url.IndexOf('&', valueStart);
        var keyValue = valueEnd < 0 ? url[valueStart..] : url[valueStart..valueEnd];
        return url.Replace($"api_key={keyValue}", "api_key=***");
    }
}
