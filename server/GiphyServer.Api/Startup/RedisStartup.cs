using GiphyServer.Api.Cache;
using StackExchange.Redis;

namespace GiphyServer.Api.Startup;

public static class RedisStartup
{
    public static WebApplicationBuilder AddRedis(this WebApplicationBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
                               ?? builder.Configuration["Redis:ConnectionString"]
                               ?? "localhost:6379";

        builder.Services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(connectionString));

        builder.Services.AddSingleton<ICacheService, RedisCacheService>();

        return builder;
    }
}
