using GiphyServer.Api.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GiphyServer.Api.Startup;

/// <summary>
/// Translates domain exceptions to RFC 7807 ProblemDetails responses.
/// Registered via AddExceptionHandler&lt;GlobalExceptionHandler&gt;() and activated by UseExceptionHandler().
/// Never exposes stack traces or internal details to callers.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) =>
        _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, problemType, title, level) = exception switch
        {
            GiphyAuthenticationException => (401, "giphy-authentication", "Giphy authentication failed.",    LogLevel.Warning),
            GiphyRateLimitException      => (429, "giphy-rate-limit",     "Giphy rate limit exceeded.",      LogLevel.Warning),
            GiphyUnavailableException    => (502, "giphy-unavailable",    "Giphy service is unavailable.",   LogLevel.Warning),
            _                            => (500, "internal-error",       "Unexpected server error.",         LogLevel.Error),
        };

        _logger.Log(level, exception,
            "Exception {ExceptionType} mapped to HTTP {StatusCode}", exception.GetType().Name, status);

        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Type   = $"https://api.speria.dev/problems/{problemType}",
            Title  = title,
            Status = status,
            Detail = "An unexpected error occurred.",
        };

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
