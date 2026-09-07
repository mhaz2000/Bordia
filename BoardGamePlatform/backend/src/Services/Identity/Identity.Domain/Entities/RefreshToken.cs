using BuildingBlocks.Domain.Base;

namespace Identity.Domain.Entities;

/// <summary>
/// Represents a refresh token issued to a user for obtaining new access tokens.
/// </summary>
public class RefreshToken : AuditableEntity
{
    private RefreshToken()
    {
    }

    /// <summary>
    /// Creates a new refresh token bound to a user.
    /// </summary>
    public static RefreshToken Create(Guid userId, string token, DateTime expiresAt)
    {
        return new RefreshToken
        {
            UserId = userId,
            Token = token,
            ExpiresAt = expiresAt
        };
    }

    /// <summary>
    /// The user the token was issued to.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// The opaque refresh token value.
    /// </summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the token expires.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// UTC timestamp when the token was revoked. Null if still valid.
    /// </summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>
    /// The token that replaced this one after a refresh. Null if never replaced.
    /// </summary>
    public string? ReplacedByToken { get; private set; }

    /// <summary>
    /// Indicates whether the token is still usable.
    /// </summary>
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;

    /// <summary>
    /// Revokes this token, optionally recording the replacing token.
    /// </summary>
    public void Revoke(string? replacedByToken = null)
    {
        RevokedAt = DateTime.UtcNow;
        ReplacedByToken = replacedByToken;
    }
}