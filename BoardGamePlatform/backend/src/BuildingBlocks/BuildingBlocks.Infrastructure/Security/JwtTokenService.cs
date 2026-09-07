using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Generates and validates JSON Web Tokens (JWT) for access and refresh tokens.
/// </summary>
public class JwtTokenService
{
    private readonly JwtOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtTokenService"/> class.
    /// </summary>
    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Generates a signed JWT access token for the specified user and claims.
    /// </summary>
    /// <param name="userId">The user's id (used as the subject claim).</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="displayName">The user's display name.</param>
    /// <param name="claims">Optional additional claims to include.</param>
    public string GenerateAccessToken(Guid userId, string email, string displayName, IDictionary<string, string>? claims = null)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var tokenClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Name, displayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        if (claims is not null)
        {
            foreach (var claim in claims)
            {
                tokenClaims.Add(new Claim(claim.Key, claim.Value));
            }
        }

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: tokenClaims,
            notBefore: now,
            expires: now.AddMinutes(_options.AccessTokenExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generates a cryptographically random refresh token value.
    /// </summary>
    public string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    /// <summary>
    /// Extracts the user id from a valid, unexpired JWT access token string.
    /// Returns null if the token is invalid or cannot be parsed.
    /// </summary>
    public Guid? GetUserIdFromToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            var sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;

            return Guid.TryParse(sub, out var userId) ? userId : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
