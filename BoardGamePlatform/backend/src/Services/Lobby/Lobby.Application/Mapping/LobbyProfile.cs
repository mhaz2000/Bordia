using AutoMapper;
using Lobby.Application.Dtos;
using Lobby.Domain.Entities;

namespace Lobby.Application.Mapping;

/// <summary>
/// AutoMapper profile mapping lobby domain entities to DTOs.
/// </summary>
public class LobbyProfile : Profile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LobbyProfile"/> class.
    /// </summary>
    public LobbyProfile()
    {
        CreateMap<Room, LobbyRoomDto>()
            .ForMember(
                d => d.Status,
                opt => opt.MapFrom(s => s.Status.ToString()));

        CreateMap<RoomPlayer, LobbyRoomPlayerDto>()
            .ForMember(
                d => d.IsConnected,
                opt => opt.MapFrom(s => !string.IsNullOrEmpty(s.ConnectionId)));
    }
}