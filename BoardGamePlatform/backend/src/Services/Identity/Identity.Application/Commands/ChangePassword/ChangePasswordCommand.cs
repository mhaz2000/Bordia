using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;

namespace Identity.Application.Commands.ChangePassword;

/// <summary>
/// Changes the current user's password.
/// </summary>
public record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand;