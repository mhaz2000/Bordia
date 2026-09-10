using AutoMapper;
using BuildingBlocks.Application;
using BuildingBlocks.Domain.Results;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Localization;
using Lobby.Application.Dtos;
using Lobby.Application.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Lobby.Application.Queries.GetMyRooms;

/// <summary>
/// Handler for <see cref="GetMyRoomsQuery"/>.
/// </summary>
public class GetMyRoomsQueryHandler : IRequestHandler<GetMyRoomsQuery, Result<List<LobbyRoomDto>>>
{
    private readonly LobbyDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetMyRoomsQueryHandler"/> class.
    /// </summary>
    public GetMyRoomsQueryHandler(
        LobbyDbContext dbContext,
        IMapper mapper,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<Result<List<LobbyRoomDto>>> Handle(
        GetMyRoomsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedException(ErrorCodes.Common.NotAuthenticated);
        }

        var rooms = await _dbContext.Rooms
            .AsNoTracking()
            .Include(r => r.Players)
            .Where(r => r.Players.Any(p => p.UserId == userId) && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return Result<List<LobbyRoomDto>>.Success(_mapper.Map<List<LobbyRoomDto>>(rooms));
    }
}