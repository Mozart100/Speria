using GiphyServer.Api.Cache;
using GiphyServer.Api.Services;
using Microsoft.Extensions.Options;

namespace GiphyServer.Api.Startup;

public static class CacheServiceRegistration
{
    /// <summary>
    /// Registers the decorator chain:
    ///   IGifService → CachingGiphyServiceDecorator → GiphyGifService
    /// </summary>
    public static IServiceCollection AddGifServices(this IServiceCollection services)
    {
        // Register the real implementation under its concrete type so the decorator can resolve it.
        services.AddScoped<GiphyGifService>();

        // IGifService resolves to the caching decorator, which wraps GiphyGifService.
        services.AddScoped<IGifService>(sp => new CachingGiphyServiceDecorator(
            inner: sp.GetRequiredService<GiphyGifService>(),
            cache: sp.GetRequiredService<ICacheService>(),
            options: sp.GetRequiredService<IOptions<CacheOptions>>(),
            logger: sp.GetRequiredService<ILogger<CachingGiphyServiceDecorator>>()));

        return services;
    }
}
