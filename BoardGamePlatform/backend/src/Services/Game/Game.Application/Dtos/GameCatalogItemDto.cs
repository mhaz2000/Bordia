namespace Game.Application.Dtos;

/// <summary>
/// Describes a game available on the platform.
/// </summary>
public sealed record GameCatalogItemDto(string GameType, int MinPlayers, int MaxPlayers);