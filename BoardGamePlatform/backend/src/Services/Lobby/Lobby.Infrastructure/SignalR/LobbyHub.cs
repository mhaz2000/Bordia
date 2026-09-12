using BuildingBlocks.Contracts.SignalR;

namespace Lobby.Infrastructure.SignalR;

/// <summary>
/// Group naming conventions for the lobby hub.
/// </summary>
public static class LobbyRoomGroups
{
    /// <summary>
    /// The group name clients of a room are added to.
    /// </summary>
    public static string For(Guid roomId) => $"lobby-room-{roomId}";
}

/// <summary>
/// SignalR hub for the waiting room.
/// Contains no business logic; it only manages group membership and forwards
/// connection bookkeeping to the Application layer.
/// </summary>
[Microsoft.AspNetCore.Authorization.Authorize]
public class LobbyHub : Microsoft.AspNetCore.SignalR.Hub<ILobbyHubClient>
{
    private readonly MediatR.IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="LobbyHub"/> class.
    /// </summary>
    public LobbyHub(MediatR.IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Joins the client's connection to the room's group and records the connection id.
    /// </summary>
    /// <param name="roomId">The id of the room to join.</param>
    public async Task JoinRoom(Guid roomId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, LobbyRoomGroups.For(roomId));

        await _mediator.Send(new Lobby.Application.Commands.SetPlayerConnection.SetPlayerConnectionCommand(
            roomId,
            Context.ConnectionId));
    }

    /// <summary>
    /// Removes the client's connection from the room's group.
    /// </summary>
    /// <param name="roomId">The id of the room to leave.</param>
    public async Task LeaveRoom(Guid roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, LobbyRoomGroups.For(roomId));
    }

    /// <summary>
    /// Marks the player's seats offline when the connection drops (tab close,
    /// network loss), so the waiting room presence stays truthful.
    /// </summary>
    /// <param name="exception">The exception that ended the connection, if any.</param>
    public override async Task OnDisconnectedAsync(System.Exception? exception)
    {
        await _mediator.Send(new Lobby.Application.Commands.ClearPlayerConnection.ClearPlayerConnectionCommand(
            Context.ConnectionId));

        await base.OnDisconnectedAsync(exception);
    }
}