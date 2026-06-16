using AutoMapper;
using GiphyServer.Api.Models;

namespace GiphyServer.Api.Startup;

/// <summary>
/// AutoMapper profile that defines all mappings between internal Giphy API DTOs
/// and the application's public response models.
/// </summary>
public sealed class AutoMapperProfile : Profile
{
    /// <summary>Configures all mappings for the GiphyServer.Api assembly.</summary>
    public AutoMapperProfile()
    {
        // A single Giphy GIF DTO → a single URL response: extract the original image URL.
        CreateMap<GiphyGifDto, GifUrlResponse>()
            .ForMember(
                dest => dest.Url,
                opt => opt.MapFrom(src => src.Images.Original.Url));

        // The Giphy response envelope → the collection response: map the Data list.
        CreateMap<GiphyResponse, GifUrlsResponse>()
            .ForMember(
                dest => dest.Gifs,
                opt => opt.MapFrom(src => src.Data));
    }
}
