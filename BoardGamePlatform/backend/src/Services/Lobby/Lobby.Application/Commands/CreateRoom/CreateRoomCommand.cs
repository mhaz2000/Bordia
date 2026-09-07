using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Dtos;

namespace Lobby.Application.Commands.CreateRoom;

/// <summary>
/// Creates a new lobby room with the current user as host.
/// </summary>
public record CreateRoomCommand(
    string Name,
    string GameType,
    int MaxPlayers,
    bool IsPrivate = false,
    string? SettingsJson = null) : ICommand<LobbyRoomDto>;