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

    /// <summary>
    /// Initialises a new instance of <see cref="GiphyHttpClient"/>.
    /// The <see cref="HttpClient"/> base address is configured during DI registration.
    /// </summary>
    public GiphyHttpClient(HttpClient httpClient, IOptions<GiphyOptions> options)
    {
        _httpClient = httpClient;
        _apiKey = options.Value.ApiKey;
    }

    /// <inheritdoc/>
    public Task<GiphyResponse> GetTrendingAsync(CancellationToken cancellationToken = default)
    {
        var url = $"gifs/trending?api_key={_apiKey}&limit=20";
        return FetchAsync(url, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<GiphyResponse> SearchAsync(string term, CancellationToken cancellationToken = default)
    {
        var encodedTerm = Uri.EscapeDataString(term);
        var url = $"gifs/search?api_key={_apiKey}&q={encodedTerm}&limit=20";
        return FetchAsync(url, cancellationToken);
    }

    private async Task<GiphyResponse> FetchAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GiphyResponse>(cancellationToken: cancellationToken);
        return result ?? new GiphyResponse();
    }
}
