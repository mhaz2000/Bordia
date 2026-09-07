using BuildingBlocks.Domain.Base;

namespace Identity.Domain.Entities;

/// <summary>
/// Represents a registered user of the platform.
/// </summary>
public class User : AuditableEntity
{
    private User()
    {
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    public static User Create(string email, string passwordHash, string displayName)
    {
        return new User
        {
            Email = email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            DisplayName = displayName,
            IsActive = true
        };
    }

    /// <summary>
    /// The user's email address (lowercased, unique).
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// The hashed password. Never store plaintext passwords.
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>
    /// The display name shown to other players.
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of the last successful login.
    /// </summary>
    public DateTime? LastLoginAt { get; private set; }

    /// <summary>
    /// Indicates whether the account is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Updates the user's display name.
    /// </summary>
    public void UpdateDisplayName(string displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName) && displayName != DisplayName)
        {
            DisplayName = displayName;
        }
    }

    /// <summary>
    /// Updates the user's password hash.
    /// </summary>
    public void UpdatePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
    }

    /// <summary>
    /// Records a successful login timestamp.
    /// </summary>
    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }
}