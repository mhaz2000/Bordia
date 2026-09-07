namespace BuildingBlocks.Contracts.Events;

/// <summary>
/// Raised when a new user successfully registers with the platform.
/// Published by the Identity Service.
/// </summary>
public class UserRegistered : IntegrationEvent
{
    /// <summary>
    /// The id of the newly registered user.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The user's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The user's display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
