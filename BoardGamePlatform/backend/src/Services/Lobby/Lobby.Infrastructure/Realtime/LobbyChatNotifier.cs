using BuildingBlocks.Contracts.SignalR;
using Lobby.Application.Realtime;
using Lobby.Infrastructure.SignalR;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace Lobby.Infrastructure.Realtime;

/// <summary>
/// Pushes room chat messages to the lobby SignalR group. Chat never touches
/// the room caches, so this is a dedicated notification handler separate from
/// <see cref="LobbyRealTimeNotifier"/>.
/// </summary>
public class LobbyChatNotifier : INotificationHandler<RoomChatMessageSent>
{
    private readonly IHubContext<LobbyHub, ILobbyHubClient> _hub;

    /// <summary>
    /// Initializes a new instance of the <see cref="LobbyChatNotifier"/> class.
    /// </summary>
    public LobbyChatNotifier(IHubContext<LobbyHub, ILobbyHubClient> hub)
    {
        _hub = hub;
    }

    /// <inheritdoc />
    public async Task Handle(RoomChatMessageSent notification, CancellationToken cancellationToken)
    {
        var message = notification.Message;
        await _hub.Clients
            .Group(LobbyRoomGroups.For(notification.RoomId))
            .RoomMessage(
                notification.RoomId,
                message.Id,
                message.UserId,
                message.DisplayName,
                message.Text,
                message.SentAt);
    }
}
