using AutoMapper;
using GiphyServer.Api.Clients;
using GiphyServer.Api.Models;

namespace GiphyServer.Api.Services;

/// <summary>
/// <see cref="IGifService"/> implementation that retrieves GIFs from the Giphy API
/// and projects the response into application models via AutoMapper.
/// </summary>
public sealed class GiphyGifService : IGifService
{
    private readonly IGiphyClient _giphyClient;
    private readonly IMapper _mapper;

    /// <summary>Initialises a new instance of <see cref="GiphyGifService"/>.</summary>
    public GiphyGifService(IGiphyClient giphyClient, IMapper mapper)
    {
        _giphyClient = giphyClient;
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<GifUrlsResponse> GetTrendingAsync(CancellationToken cancellationToken = default)
    {
        var response = await _giphyClient.GetTrendingAsync(cancellationToken);
        return _mapper.Map<GifUrlsResponse>(response);
    }

    /// <inheritdoc/>
    public async Task<GifUrlsResponse> SearchAsync(string term, CancellationToken cancellationToken = default)
    {
        var response = await _giphyClient.SearchAsync(term, cancellationToken);
        return _mapper.Map<GifUrlsResponse>(response);
    }
}
