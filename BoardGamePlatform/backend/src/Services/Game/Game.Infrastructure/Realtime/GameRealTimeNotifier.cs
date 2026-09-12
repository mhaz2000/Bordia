using BuildingBlocks.Contracts.SignalR;
using Game.Application.Common;
using Game.Application.Persistence;
using Game.Application.Realtime;
using Game.Domain.Entities;
using Game.Domain.Enums;
using Game.Infrastructure.SignalR;
using GameEngine.Core;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Game.Infrastructure.Realtime;

/// <summary>
/// Pushes game changes to the game SignalR hub. This is the single place where
/// <see cref="GameStateChanged"/> notifications are translated into client calls.
/// State updates are projected per seat when the session's engine implements
/// <see cref="IPlayerViewGame"/> (hidden-information games); otherwise the full
/// state is broadcast to the session group as before.
/// </summary>
public class GameRealTimeNotifier : INotificationHandler<GameStateChanged>
{
    private readonly IHubContext<GameHub, IGameHubClient> _hub;
    private readonly IGameEngineProvider _engineProvider;
    private readonly GameDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameRealTimeNotifier"/> class.
    /// </summary>
    public GameRealTimeNotifier(
        IHubContext<GameHub, IGameHubClient> hub,
        IGameEngineProvider engineProvider,
        GameDbContext dbContext)
    {
        _hub = hub;
        _engineProvider = engineProvider;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task Handle(GameStateChanged notification, CancellationToken cancellationToken)
    {
        return notification.ChangeType switch
        {
            GameChangeType.StateUpdated => PushStateAsync(notification, cancellationToken),

            GameChangeType.ActionProcessed => ClientsOf(notification.SessionId).ActionProcessed(
                notification.SessionId,
                notification.PlayerId!.Value,
                notification.ActionType ?? string.Empty),

            GameChangeType.PlayerConnectionChanged => ClientsOf(notification.SessionId).PlayerConnectionChanged(
                notification.SessionId,
                notification.PlayerId!.Value,
                notification.IsConnected),

            GameChangeType.GameFinished => ClientsOf(notification.SessionId).GameFinished(
                notification.SessionId,
                notification.WinnerId),

            GameChangeType.PlayerTakenOver => notification.PlayerId is { } playerId
                    && notification.ConnectionId is { Length: > 0 } connectionId
                ? _hub.Clients.Client(connectionId).SessionTakenOver(notification.SessionId, playerId)
                : Task.CompletedTask,

            _ => Task.CompletedTask
        };
    }

    private IGameHubClient ClientsOf(Guid sessionId)
        => _hub.Clients.Group(GameSessionGroups.For(sessionId));

    private async Task PushStateAsync(GameStateChanged notification, CancellationToken cancellationToken)
    {
        if (notification.State is null)
        {
            return;
        }

        if (notification.GameType is not { } gameType
            || _engineProvider.Get(gameType) is not IPlayerViewGame viewGame)
        {
            // Full-state engine (e.g. UNO): one group broadcast, as before.
            await ClientsOf(notification.SessionId).GameStateUpdated(
                notification.SessionId,
                notification.State);
            return;
        }

        // Hidden-information engine: send every connected seat its own view.
        var seats = await _dbContext.GamePlayers.AsNoTracking()
            .Where(p => p.GameSessionId == notification.SessionId)
            .Select(p => new { p.UserId, p.ConnectionId })
            .ToListAsync(cancellationToken);

        foreach (var seat in seats)
        {
            if (string.IsNullOrEmpty(seat.ConnectionId)) continue;
            var view = viewGame.GetPlayerView(notification.State, new GameEngine.Core.Models.PlayerId(seat.UserId));
            await _hub.Clients.Client(seat.ConnectionId).GameStateUpdated(notification.SessionId, view);
        }
    }
}
