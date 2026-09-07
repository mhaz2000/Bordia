namespace Identity.Application.Dtos;

/// <summary>
/// Public-facing user profile data.
/// </summary>
public class UserProfileDto
{
    /// <summary>
    /// The user's id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The user's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The user's display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of the last login, if any.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// UTC timestamp when the account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}