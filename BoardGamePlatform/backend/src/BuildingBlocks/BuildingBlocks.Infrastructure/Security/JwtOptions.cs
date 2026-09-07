namespace BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Configuration options for JWT token generation and validation.
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// The JWT issuer.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// The JWT audience.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// The signing key used to generate and validate tokens.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Lifetime of the access token in minutes.
    /// </summary>
    public int AccessTokenExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// Lifetime of the refresh token in days.
    /// </summary>
    public int RefreshTokenExpiryDays { get; set; } = 7;
}
