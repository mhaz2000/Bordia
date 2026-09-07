using BuildingBlocks.Application.CQRS;
using Identity.Application.Dtos;

namespace Identity.Application.Commands.UpdateProfile;

/// <summary>
/// Updates the current user's profile.
/// </summary>
public record UpdateProfileCommand(string DisplayName) : ICommand<UserProfileDto>;