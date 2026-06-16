namespace GiphyServer.Api.Startup;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public int TtlMinutes { get; init; } = 60;
}
