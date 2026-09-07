using AutoMapper;
using BuildingBlocks.Application;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using Game.Application.Dtos;
using Game.Application.Persistence;
using Game.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Game.Application.Commands.PauseGame;

/// <summary>
/// Handler that pauses an active session.
/// </summary>
public class PauseGameCommandHandler : IRequestHandler<PauseGameCommand, Result<GameSessionDto>>
{
    private readonly GameDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="PauseGameCommandHandler"/> class.
    /// </summary>
    public PauseGameCommandHandler(
        GameDbContext dbContext,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<GameSessionDto>> Handle(
        PauseGameCommand request,
        CancellationToken cancellationToken)
    {
        var session = await _dbContext.GameSessions
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(GameSession), request.SessionId);

        session.Pause();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<GameSessionDto>.Success(_mapper.Map<GameSessionDto>(session));
    }
}