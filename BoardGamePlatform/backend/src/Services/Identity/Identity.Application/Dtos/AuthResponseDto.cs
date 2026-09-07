namespace Identity.Application.Dtos;

/// <summary>
/// Token pair returned after authentication operations.
/// </summary>
public class AuthResponseDto
{
    /// <summary>
    /// The JWT access token.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// The refresh token used to obtain new access tokens.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Lifetime of the access token in seconds.
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// The authenticated user's profile.
    /// </summary>
    public UserProfileDto User { get; set; } = new();
}