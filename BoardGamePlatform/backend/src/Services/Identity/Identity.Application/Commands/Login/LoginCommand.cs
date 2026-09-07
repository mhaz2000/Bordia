using BuildingBlocks.Application.CQRS;
using Identity.Application.Dtos;

namespace Identity.Application.Commands.Login;

/// <summary>
/// Authenticates a user and returns a token pair.
/// </summary>
public record LoginCommand(string Identifier, string Password) : ICommand<AuthResponseDto>;