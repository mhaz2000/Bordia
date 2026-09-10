using AutoMapper;
using BuildingBlocks.Application;
using BuildingBlocks.Domain.Results;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Localization;
using Game.Application.Dtos;
using Game.Application.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Game.Application.Queries.GetPlayerGameSessions;

/// <summary>
/// Handler for <see cref="GetPlayerGameSessionsQuery"/>.
/// </summary>
public class GetPlayerGameSessionsQueryHandler
    : IRequestHandler<GetPlayerGameSessionsQuery, Result<List<GameSessionDto>>>
{
    private readonly GameDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetPlayerGameSessionsQueryHandler"/> class.
    /// </summary>
    public GetPlayerGameSessionsQueryHandler(
        GameDbContext dbContext,
        IMapper mapper,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<Result<List<GameSessionDto>>> Handle(
        GetPlayerGameSessionsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedException(ErrorCodes.Common.NotAuthenticated);
        }

        var sessions = await _dbContext.GameSessions
            .AsNoTracking()
            .Include(s => s.Players)
            .Where(s => s.Players.Any(p => p.UserId == userId) && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return Result<List<GameSessionDto>>.Success(_mapper.Map<List<GameSessionDto>>(sessions));
    }
}