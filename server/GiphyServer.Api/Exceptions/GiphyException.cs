namespace GiphyServer.Api.Exceptions;

public abstract class GiphyException : Exception
{
    protected GiphyException(string message, Exception? inner = null)
        : base(message, inner) { }
}

/// <summary>Giphy returned 401 or 403 — API key is invalid or revoked.</summary>
public sealed class GiphyAuthenticationException : GiphyException
{
    public GiphyAuthenticationException(string message, Exception? inner = null)
        : base(message, inner) { }
}

/// <summary>Giphy returned 429 — the API rate limit has been exceeded.</summary>
public sealed class GiphyRateLimitException : GiphyException
{
    public GiphyRateLimitException(string message, Exception? inner = null)
        : base(message, inner) { }
}

/// <summary>Giphy is unreachable — connection failure, circuit breaker open, or 5xx response.</summary>
public sealed class GiphyUnavailableException : GiphyException
{
    public GiphyUnavailableException(string message, Exception? inner = null)
        : base(message, inner) { }
}
