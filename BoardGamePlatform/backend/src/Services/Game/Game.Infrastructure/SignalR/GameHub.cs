using BuildingBlocks.Contracts.SignalR;
using Game.Application.Commands.ReconnectPlayer;

namespace Game.Infrastructure.SignalR;

/// <summary>
/// Group naming conventions for the game hub.
/// </summary>
public static class GameSessionGroups
{
    /// <summary>
    /// The group name clients of a session are added to.
    /// </summary>
    public static string For(Guid sessionId) => $"game-session-{sessionId}";
}

/// <summary>
/// SignalR hub for live game sessions.
/// Contains no business logic; it only manages group membership and forwards
/// connection bookkeeping to the Application layer.
/// </summary>
[Microsoft.AspNetCore.Authorization.Authorize]
public class GameHub : Microsoft.AspNetCore.SignalR.Hub<IGameHubClient>
{
    private readonly MediatR.IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameHub"/> class.
    /// </summary>
    public GameHub(MediatR.IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Adds the client's connection to the session's group and records the connection id.
    /// </summary>
    /// <param name="sessionId">The id of the game session to join.</param>
    public async Task JoinSession(Guid sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GameSessionGroups.For(sessionId));

        await _mediator.Send(new ReconnectPlayerCommand(sessionId, Context.ConnectionId));
    }

    /// <summary>
    /// Removes the client's connection from the session's group.
    /// </summary>
    /// <param name="sessionId">The id of the game session to leave.</param>
    public async Task LeaveSession(Guid sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GameSessionGroups.For(sessionId));
    }
}