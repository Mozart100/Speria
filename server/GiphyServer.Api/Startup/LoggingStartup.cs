namespace GiphyServer.Api.Startup;

public static class LoggingStartup
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
        => app.UseMiddleware<RequestLoggingMiddleware>();
}
