using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Game.Application.Dtos;

namespace Game.Application.Queries.GetGameSession;

/// <summary>
/// Fetches a game session by id.
/// </summary>
public record GetGameSessionQuery(Guid SessionId) : IQuery<GameSessionDto>;