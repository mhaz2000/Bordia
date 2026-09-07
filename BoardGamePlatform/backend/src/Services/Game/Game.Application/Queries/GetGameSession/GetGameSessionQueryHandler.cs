using AutoMapper;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using Game.Application.Dtos;
using Game.Application.Persistence;
using Game.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Game.Application.Queries.GetGameSession;

/// <summary>
/// Handler for <see cref="GetGameSessionQuery"/>.
/// </summary>
public class GetGameSessionQueryHandler : IRequestHandler<GetGameSessionQuery, Result<GameSessionDto>>
{
    private readonly GameDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetGameSessionQueryHandler"/> class.
    /// </summary>
    public GetGameSessionQueryHandler(
        GameDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<GameSessionDto>> Handle(
        GetGameSessionQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _dbContext.GameSessions
            .AsNoTracking()
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(GameSession), request.SessionId);

        return Result<GameSessionDto>.Success(_mapper.Map<GameSessionDto>(session));
    }
}