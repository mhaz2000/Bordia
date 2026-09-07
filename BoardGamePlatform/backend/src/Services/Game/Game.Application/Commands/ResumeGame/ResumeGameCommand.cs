using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Game.Application.Dtos;

namespace Game.Application.Commands.ResumeGame;

/// <summary>
/// Resumes a paused game session.
/// </summary>
public record ResumeGameCommand(Guid SessionId) : ICommand<GameSessionDto>;