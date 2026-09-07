using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Dtos;
using Lobby.Domain.Enums;

namespace Lobby.Application.Commands.StartGame;

/// <summary>
/// Starts the game from a waiting room (host only).
/// </summary>
public record StartGameCommand(Guid RoomId) : ICommand<LobbyRoomDto>;