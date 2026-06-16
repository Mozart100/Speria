using GiphyServer.Api.Cache;
using GiphyServer.Api.Models;
using GiphyServer.Api.Startup;
using Microsoft.Extensions.Options;

namespace GiphyServer.Api.Services;

/// <summary>
/// Decorator around <see cref="IGifService"/> that adds Redis caching.
/// Architecture: IGifService → CachingGiphyServiceDecorator → GiphyGifService
/// </summary>
public sealed class CachingGiphyServiceDecorator : IGifService
{
    private const string TrendingKey = "giphy:trending:today";

    private readonly IGifService _inner;
    private readonly ICacheService _cache;
    private readonly TimeSpan _ttl;
    private readonly ILogger<CachingGiphyServiceDecorator> _logger;

    public CachingGiphyServiceDecorator(
        IGifService inner,
        ICacheService cache,
        IOptions<CacheOptions> options,
        ILogger<CachingGiphyServiceDecorator> logger)
    {
        _inner = inner;
        _cache = cache;
        _ttl = TimeSpan.FromMinutes(options.Value.TtlMinutes);
        _logger = logger;
    }

    public Task<GifUrlsResponse> GetTrendingAsync(CancellationToken cancellationToken = default)
        => GetOrSetAsync(TrendingKey, () => _inner.GetTrendingAsync(cancellationToken));

    public Task<GifUrlsResponse> SearchAsync(string term, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"giphy:search:{NormalizeTerm(term)}";
        return GetOrSetAsync(cacheKey, () => _inner.SearchAsync(term, cancellationToken));
    }

    private async Task<GifUrlsResponse> GetOrSetAsync(string cacheKey, Func<Task<GifUrlsResponse>> fetch)
    {
        var cached = await _cache.GetAsync<GifUrlsResponse>(cacheKey);
        if (cached is not null)
        {
            _logger.LogInformation("Cache hit for key {CacheKey}", cacheKey);

            foreach (var gif in cached.Gifs)
                _logger.LogInformation("GIF URL {GifUrl} source = Cache", gif.Url);

            return cached;
        }

        _logger.LogInformation("Cache miss for key {CacheKey}. Calling Giphy API.", cacheKey);

        var result = await fetch();

        foreach (var gif in result.Gifs)
            _logger.LogInformation("GIF URL {GifUrl} source = Giphy API", gif.Url);

        _logger.LogInformation(
            "Writing {Count} GIF URLs into Redis cache using key {CacheKey}. TTL={TTL}",
            result.Gifs.Count,
            cacheKey,
            _ttl);

        foreach (var gif in result.Gifs)
            _logger.LogInformation("Caching URL: {GifUrl}", gif.Url);

        await _cache.SetAsync(cacheKey, result, _ttl);

        return result;
    }

    // Trim, lowercase, collapse duplicate spaces — produces a stable cache key.
    private static string NormalizeTerm(string term)
        => string.Join(' ', term.Trim().ToLowerInvariant()
               .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
