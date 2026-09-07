using AutoMapper;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Dtos;
using Lobby.Application.Persistence;
using Lobby.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Lobby.Application.Queries.GetRoom;

/// <summary>
/// Handler for <see cref="GetRoomQuery"/>.
/// </summary>
public class GetRoomQueryHandler : IRequestHandler<GetRoomQuery, Result<LobbyRoomDto>>
{
    private readonly LobbyDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetRoomQueryHandler"/> class.
    /// </summary>
    public GetRoomQueryHandler(
        LobbyDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<LobbyRoomDto>> Handle(
        GetRoomQuery request,
        CancellationToken cancellationToken)
    {
        var room = await _dbContext.Rooms
            .Include(r => r.Players)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RoomId && !r.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Room), request.RoomId);

        return Result<LobbyRoomDto>.Success(_mapper.Map<LobbyRoomDto>(room));
    }
}