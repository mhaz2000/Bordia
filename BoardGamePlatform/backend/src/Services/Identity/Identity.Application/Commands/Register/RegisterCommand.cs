using BuildingBlocks.Application.CQRS;
using Identity.Application.Dtos;

namespace Identity.Application.Commands.Register;

/// <summary>
/// Registers a new user account and returns a token pair.
/// </summary>
public record RegisterCommand(string Email, string Password, string DisplayName)
    : ICommand<AuthResponseDto>;