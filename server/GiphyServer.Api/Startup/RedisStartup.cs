using GiphyServer.Api.Cache;
using StackExchange.Redis;

namespace GiphyServer.Api.Startup;

public static class RedisStartup
{
    public static WebApplicationBuilder AddRedis(this WebApplicationBuilder builder, string connectionString)
    {
        builder.Services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(connectionString));

        builder.Services.AddSingleton<ICacheService, RedisCacheService>();

        return builder;
    }
}
