using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using BuildingBlocks.Infrastructure.Caching;
using Game.Application.Common;
using Game.Application.Persistence;
using Game.Domain.Entities;
using GameEngine.Core.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Game.Application.Queries.GetGameState;

/// <summary>
/// Handler for <see cref="GetGameStateQuery"/>; reads from Redis when available.
/// </summary>
public class GetGameStateQueryHandler : IRequestHandler<GetGameStateQuery, Result<GameState>>
{
    private readonly GameDbContext _dbContext;
    private readonly RedisCacheService _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetGameStateQueryHandler"/> class.
    /// </summary>
    public GetGameStateQueryHandler(
        GameDbContext dbContext,
        RedisCacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<Result<GameState>> Handle(
        GetGameStateQuery request,
        CancellationToken cancellationToken)
    {
        var cachedJson = await _cache.GetAsync<string>(
            GameCacheKeys.State(request.SessionId),
            cancellationToken);

        if (cachedJson is not null)
        {
            return Result<GameState>.Success(GameState.FromJson(cachedJson));
        }

        var session = await _dbContext.GameSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(GameSession), request.SessionId);

        return Result<GameState>.Success(GameState.FromJson(session.CurrentStateJson));
    }
}