using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using GameEngine.Core.Models;

namespace Game.Application.Queries.GetGameState;

/// <summary>
/// Fetches the current state of a game session.
/// </summary>
public record GetGameStateQuery(Guid SessionId) : IQuery<GameState>;