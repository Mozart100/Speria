using System.Diagnostics;

namespace GiphyServer.Api.Startup;

public sealed class RequestLoggingMiddleware
{
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Cookie", "Set-Cookie",
        "X-Api-Key", "Api-Key", "X-Auth-Token", "X-Access-Token",
        "Proxy-Authorization"
    };

    private const int MaxBodyBytes = 65_536; // 64 KB

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = context.Request;

        var safeHeaders = request.Headers
            .Where(h => !SensitiveHeaders.Contains(h.Key))
            .ToDictionary(h => h.Key, h => h.Value.ToString());

        var requestBody = await ReadBodyAsync(request);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            _logger.LogInformation(
                "HTTP {Method} {Path}{QueryString} => {StatusCode} in {ElapsedMs}ms | Route: {Route} | Headers: {@Headers} | Body: {Body}",
                request.Method,
                request.Path,
                request.QueryString,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                context.GetEndpoint()?.DisplayName ?? "unknown",
                safeHeaders,
                requestBody);
        }
    }

    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        var contentLength = request.ContentLength ?? 0;
        if (contentLength == 0)
            return string.Empty;

        if (contentLength > MaxBodyBytes)
            return "[body omitted: too large]";

        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }
}
