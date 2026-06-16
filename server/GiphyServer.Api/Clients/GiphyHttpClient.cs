using GiphyServer.Api.Configuration;
using GiphyServer.Api.Exceptions;
using GiphyServer.Api.Models;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using System.Net;

namespace GiphyServer.Api.Clients;

/// <summary>
/// HTTP implementation of <see cref="IGiphyClient"/> backed by a typed <see cref="HttpClient"/>
/// registered via HttpClientFactory. Polly retry and circuit-breaker policies are applied
/// at the HttpClient pipeline level — this class handles classification of final outcomes.
/// </summary>
public sealed class GiphyHttpClient : IGiphyClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GiphyHttpClient> _logger;

    private readonly int _limit;

    public GiphyHttpClient(HttpClient httpClient, IOptions<GiphyOptions> options, ILogger<GiphyHttpClient> logger)
    {
        _httpClient = httpClient;
        _apiKey = options.Value.ApiKey;
        _limit = options.Value.ResultLimit;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<GiphyResponse> GetTrendingAsync(CancellationToken cancellationToken = default)
    {
        var url = $"gifs/trending?api_key={_apiKey}&limit={_limit}";
        return FetchAsync(url, endpoint: "trending", searchTerm: null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<GiphyResponse> SearchAsync(string term, CancellationToken cancellationToken = default)
    {
        var encodedTerm = Uri.EscapeDataString(term);
        var url = $"gifs/search?api_key={_apiKey}&q={encodedTerm}&limit={_limit}";
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
            endpoint, searchTerm ?? "(none)", safeUrl);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex,
                "Giphy circuit breaker open for {GiphyEndpoint} | SearchTerm: {SearchTerm}",
                endpoint, searchTerm ?? "(none)");
            throw new GiphyUnavailableException("Giphy service circuit breaker is open.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Giphy request failed for {GiphyEndpoint} | SearchTerm: {SearchTerm} | Error: {ErrorMessage}",
                endpoint, searchTerm ?? "(none)", ex.Message);
            throw new GiphyUnavailableException("Failed to reach Giphy API.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;

            _logger.LogError(
                "Giphy {GiphyEndpoint} returned {StatusCode} | SearchTerm: {SearchTerm}",
                endpoint, statusCode, searchTerm ?? "(none)");

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new GiphyAuthenticationException($"Giphy API authentication failed with status {statusCode}.");

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new GiphyRateLimitException("Giphy API rate limit exceeded.");

            throw new GiphyUnavailableException($"Giphy API returned unexpected status {statusCode}.");
        }

        var result = await response.Content.ReadFromJsonAsync<GiphyResponse>(cancellationToken: cancellationToken)
                     ?? new GiphyResponse();

        _logger.LogInformation(
            "Giphy {GiphyEndpoint} succeeded | SearchTerm: {SearchTerm} | StatusCode: {StatusCode} | GifCount: {GifCount}",
            endpoint, searchTerm ?? "(none)", (int)response.StatusCode, result.Data.Count);

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
