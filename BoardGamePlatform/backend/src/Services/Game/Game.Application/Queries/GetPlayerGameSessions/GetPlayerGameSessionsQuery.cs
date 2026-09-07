using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Game.Application.Dtos;

namespace Game.Application.Queries.GetPlayerGameSessions;

/// <summary>
/// Fetches the game sessions the current user is a player in.
/// </summary>
public record GetPlayerGameSessionsQuery : IQuery<List<GameSessionDto>>;