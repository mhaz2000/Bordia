using BuildingBlocks.Application.CQRS;
using Identity.Application.Dtos;

namespace Identity.Application.Commands.RefreshToken;

/// <summary>
/// Exchanges a valid refresh token for a new token pair.
/// </summary>
public record RefreshTokenCommand(string RefreshToken) : ICommand<AuthResponseDto>;