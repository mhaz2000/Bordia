using BuildingBlocks.Application;
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
/// Hidden-information games receive a per-viewer projection of the state.
/// </summary>
public class GetGameStateQueryHandler : IRequestHandler<GetGameStateQuery, Result<GameState>>
{
    private readonly GameDbContext _dbContext;
    private readonly RedisCacheService _cache;
    private readonly ICurrentUser _currentUser;
    private readonly IGameEngineProvider _engineProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetGameStateQueryHandler"/> class.
    /// </summary>
    public GetGameStateQueryHandler(
        GameDbContext dbContext,
        RedisCacheService cache,
        ICurrentUser currentUser,
        IGameEngineProvider engineProvider)
    {
        _dbContext = dbContext;
        _cache = cache;
        _currentUser = currentUser;
        _engineProvider = engineProvider;
    }

    /// <inheritdoc />
    public async Task<Result<GameState>> Handle(
        GetGameStateQuery request,
        CancellationToken cancellationToken)
    {
        var cachedJson = await _cache.GetAsync<string>(
            GameCacheKeys.State(request.SessionId),
            cancellationToken);

        GameState state;
        if (cachedJson is not null)
        {
            state = GameState.FromJson(cachedJson);
        }
        else
        {
            var session = await _dbContext.GameSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.SessionId && !s.IsDeleted, cancellationToken)
                ?? throw new NotFoundException(nameof(GameSession), request.SessionId);

            state = GameState.FromJson(session.CurrentStateJson);
        }

        var viewerId = _currentUser.UserId ?? Guid.Empty;
        var engine = _engineProvider.Get(state.GameType);
        return Result<GameState>.Success(PlayerViewProjection.Project(engine, state, viewerId));
    }
}