using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Dtos;
using Lobby.Application.Persistence;
using Lobby.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Lobby.Application.Queries.GetRoomMessages;

/// <summary>
/// Handler that returns the room's chat history to its members.
/// </summary>
public class GetRoomMessagesQueryHandler : IRequestHandler<GetRoomMessagesQuery, Result<List<RoomMessageDto>>>
{
    private readonly LobbyDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetRoomMessagesQueryHandler"/> class.
    /// </summary>
    public GetRoomMessagesQueryHandler(LobbyDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<Result<List<RoomMessageDto>>> Handle(
        GetRoomMessagesQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedException(BuildingBlocks.Domain.Localization.ErrorCodes.Common.NotAuthenticated);
        }

        var member = await _dbContext.RoomPlayers
            .AnyAsync(p => p.RoomId == request.RoomId && p.UserId == userId, cancellationToken);
        if (!member)
        {
            throw new ConflictException(BuildingBlocks.Domain.Localization.ErrorCodes.Lobby.NotMember);
        }

        var take = Math.Clamp(request.Take, 1, 200);
        var messages = await _dbContext.RoomMessages
            .Where(m => m.RoomId == request.RoomId)
            .OrderByDescending(m => m.SentAt)
            .Take(take)
            .OrderBy(m => m.SentAt)
            .Select(m => new RoomMessageDto
            {
                Id = m.Id,
                RoomId = m.RoomId,
                UserId = m.UserId,
                DisplayName = m.DisplayName,
                Text = m.Text,
                SentAt = m.SentAt
            })
            .ToListAsync(cancellationToken);

        return Result<List<RoomMessageDto>>.Success(messages);
    }
}
