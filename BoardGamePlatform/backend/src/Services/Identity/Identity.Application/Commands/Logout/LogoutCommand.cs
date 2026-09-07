using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;

namespace Identity.Application.Commands.Logout;

/// <summary>
/// Revokes the current user's refresh token.
/// </summary>
public record LogoutCommand(string RefreshToken) : ICommand;