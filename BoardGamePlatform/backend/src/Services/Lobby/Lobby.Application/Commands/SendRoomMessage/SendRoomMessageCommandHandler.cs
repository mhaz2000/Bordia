using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Localization;
using BuildingBlocks.Domain.Results;
using BuildingBlocks.Infrastructure.Caching;using Lobby.Application.Dtos;
using Lobby.Application.Persistence;
using Lobby.Application.Realtime;
using Lobby.Domain.Entities;
using Lobby.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Lobby.Application.Commands.SendRoomMessage;

/// <summary>
/// Handler that persists a room chat message and broadcasts it to the room,
/// enforcing membership, room state and the per-minute rate limit.
/// </summary>
public class SendRoomMessageCommandHandler : IRequestHandler<SendRoomMessageCommand, Result<RoomMessageDto>>
{
    private readonly LobbyDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly RedisCounter _counters;
    private readonly IPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendRoomMessageCommandHandler"/> class.
    /// </summary>
    public SendRoomMessageCommandHandler(
        LobbyDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        RedisCounter counters,
        IPublisher publisher)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _counters = counters;
        _publisher = publisher;
    }

    /// <inheritdoc />
    public async Task<Result<RoomMessageDto>> Handle(
        SendRoomMessageCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedException(ErrorCodes.Common.NotAuthenticated);
        }

        var room = await _dbContext.Rooms
            .Include(r => r.Players)
            .FirstOrDefaultAsync(r => r.Id == request.RoomId && !r.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Room), request.RoomId);

        if (room.Status == RoomStatus.Closed)
        {
            throw new ConflictException(ErrorCodes.Lobby.RoomNotAccepting);
        }

        var player = room.GetPlayer(userId)
            ?? throw new ConflictException(ErrorCodes.Lobby.NotMember);

        await EnforceRateLimitAsync(room.Id, userId, cancellationToken);

        var message = RoomMessage.Create(
            room.Id,
            userId,
            player.DisplayName,
            request.Text.Trim());
        _dbContext.RoomMessages.Add(message);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new RoomMessageDto
        {
            Id = message.Id,
            RoomId = message.RoomId,
            UserId = message.UserId,
            DisplayName = message.DisplayName,
            Text = message.Text,
            SentAt = message.SentAt
        };

        await _publisher.Publish(new RoomChatMessageSent
        {
            RoomId = room.Id,
            Message = dto
        }, cancellationToken);

        return Result<RoomMessageDto>.Success(dto);
    }

    private async Task EnforceRateLimitAsync(Guid roomId, Guid userId, CancellationToken cancellationToken)
    {
        // Fixed one-minute window per (room, user), counted atomically with
        // Redis INCR so concurrent senders can never both observe the same
        // count and slip past the cap.
        var bucket = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var key = $"lobby:chat:{roomId}:{userId}:{bucket}";
        var count = await _counters.IncrementAsync(key, TimeSpan.FromSeconds(70), cancellationToken);
        if (count > SendRoomMessageCommand.MaxPerMinute)
        {
            throw new ConflictException(ErrorCodes.Lobby.ChatRateLimited);
        }
    }
}
