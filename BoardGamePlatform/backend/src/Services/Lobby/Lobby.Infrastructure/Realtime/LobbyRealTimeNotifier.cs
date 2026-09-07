using BuildingBlocks.Contracts.SignalR;
using BuildingBlocks.Infrastructure.Caching;
using Lobby.Application.Common;
using Lobby.Application.Realtime;
using Lobby.Infrastructure.SignalR;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace Lobby.Infrastructure.Realtime;

/// <summary>
/// Pushes room changes to the lobby SignalR hub and invalidates the room caches.
/// This is the single place where <see cref="LobbyRoomChanged"/> notifications
/// are translated into client calls and cache invalidations.
/// </summary>
public class LobbyRealTimeNotifier : INotificationHandler<LobbyRoomChanged>
{
    private readonly IHubContext<LobbyHub, ILobbyHubClient> _hub;
    private readonly RedisCacheService _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="LobbyRealTimeNotifier"/> class.
    /// </summary>
    public LobbyRealTimeNotifier(IHubContext<LobbyHub, ILobbyHubClient> hub, RedisCacheService cache)
    {
        _hub = hub;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task Handle(LobbyRoomChanged notification, CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync(LobbyCacheKeys.RoomsList, cancellationToken);
        await _cache.RemoveAsync(LobbyCacheKeys.Room(notification.RoomId), cancellationToken);

        var group = LobbyRoomGroups.For(notification.RoomId);
        var clients = _hub.Clients.Group(group);

        await (notification.ChangeType switch
        {
            RoomChangeType.PlayerJoined => clients.PlayerJoined(
                notification.RoomId,
                notification.PlayerId!.Value,
                notification.DisplayName ?? string.Empty,
                notification.PlayerCount),

            RoomChangeType.PlayerLeft => clients.PlayerLeft(
                notification.RoomId,
                notification.PlayerId!.Value,
                notification.PlayerCount),

            RoomChangeType.PlayerReady => clients.PlayerReadyChanged(
                notification.RoomId,
                notification.PlayerId!.Value,
                true),

            RoomChangeType.PlayerUnready => clients.PlayerReadyChanged(
                notification.RoomId,
                notification.PlayerId!.Value,
                false),

            RoomChangeType.HostChanged => clients.HostChanged(
                notification.RoomId,
                notification.NewHostId!.Value),

            RoomChangeType.RoomClosed => clients.RoomClosed(notification.RoomId),

            RoomChangeType.GameStarted => clients.GameStarted(
                notification.RoomId,
                notification.GameSessionId!.Value),

            _ => Task.CompletedTask
        });
    }
}