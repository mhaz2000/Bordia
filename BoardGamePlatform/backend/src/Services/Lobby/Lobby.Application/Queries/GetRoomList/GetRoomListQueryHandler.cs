using AutoMapper;
using BuildingBlocks.Domain.Results;
using BuildingBlocks.Infrastructure.Caching;
using Lobby.Application.Common;
using Lobby.Application.Dtos;
using Lobby.Application.Persistence;
using Lobby.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Lobby.Application.Queries.GetRoomList;

/// <summary>
/// Handler for <see cref="GetRoomListQuery"/>; reads from Redis with a fallback to the database.
/// </summary>
public class GetRoomListQueryHandler : IRequestHandler<GetRoomListQuery, Result<List<LobbyRoomDto>>>
{
    private readonly LobbyDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly RedisCacheService _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetRoomListQueryHandler"/> class.
    /// </summary>
    public GetRoomListQueryHandler(
        LobbyDbContext dbContext,
        IMapper mapper,
        RedisCacheService cache)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<Result<List<LobbyRoomDto>>> Handle(
        GetRoomListQuery request,
        CancellationToken cancellationToken)
    {
        var cached = await _cache.GetAsync<List<LobbyRoomDto>>(
            LobbyCacheKeys.RoomsList,
            cancellationToken);

        if (cached is not null)
        {
            return Result<List<LobbyRoomDto>>.Success(cached);
        }

        var rooms = await _dbContext.Rooms
            .AsNoTracking()
            .Include(r => r.Players)
            .Where(r => r.Status == RoomStatus.Waiting && !r.IsDeleted)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = _mapper.Map<List<LobbyRoomDto>>(rooms);

        await _cache.SetAsync(LobbyCacheKeys.RoomsList, result, cancellationToken);

        return Result<List<LobbyRoomDto>>.Success(result);
    }
}