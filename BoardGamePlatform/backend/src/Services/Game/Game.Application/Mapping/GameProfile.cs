using AutoMapper;
using Game.Application.Dtos;
using Game.Domain.Entities;

namespace Game.Application.Mapping;

/// <summary>
/// AutoMapper profile mapping game domain entities to DTOs.
/// </summary>
public class GameProfile : Profile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GameProfile"/> class.
    /// </summary>
    public GameProfile()
    {
        CreateMap<GameSession, GameSessionDto>()
            .ForMember(
                d => d.Status,
                opt => opt.MapFrom(s => s.Status.ToString()));

        CreateMap<GamePlayer, GamePlayerDto>();
    }
}