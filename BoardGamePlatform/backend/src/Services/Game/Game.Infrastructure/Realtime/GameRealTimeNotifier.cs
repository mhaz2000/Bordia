using BuildingBlocks.Contracts.SignalR;
using Game.Application.Realtime;
using Game.Infrastructure.SignalR;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace Game.Infrastructure.Realtime;

/// <summary>
/// Pushes game changes to the game SignalR hub. This is the single place where
/// <see cref="GameStateChanged"/> notifications are translated into client calls.
/// </summary>
public class GameRealTimeNotifier : INotificationHandler<GameStateChanged>
{
    private readonly IHubContext<GameHub, IGameHubClient> _hub;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameRealTimeNotifier"/> class.
    /// </summary>
    public GameRealTimeNotifier(IHubContext<GameHub, IGameHubClient> hub)
    {
        _hub = hub;
    }

    /// <inheritdoc />
    public Task Handle(GameStateChanged notification, CancellationToken cancellationToken)
    {
        var clients = _hub.Clients.Group(GameSessionGroups.For(notification.SessionId));

        return notification.ChangeType switch
        {
            GameChangeType.StateUpdated => clients.GameStateUpdated(
                notification.SessionId,
                notification.State!),

            GameChangeType.ActionProcessed => clients.ActionProcessed(
                notification.SessionId,
                notification.PlayerId!.Value,
                notification.ActionType ?? string.Empty),

            GameChangeType.PlayerConnectionChanged => clients.PlayerConnectionChanged(
                notification.SessionId,
                notification.PlayerId!.Value,
                notification.IsConnected),

            GameChangeType.GameFinished => clients.GameFinished(
                notification.SessionId,
                notification.WinnerId),

            GameChangeType.PlayerTakenOver => notification.PlayerId is { } playerId
                    && notification.ConnectionId is { Length: > 0 } connectionId
                ? _hub.Clients.Client(connectionId).SessionTakenOver(notification.SessionId, playerId)
                : Task.CompletedTask,

            _ => Task.CompletedTask
        };
    }
}