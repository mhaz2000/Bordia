using BuildingBlocks.Infrastructure.Security;
using Identity.Application.Dtos;
using Identity.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Identity.Application.Common;

/// <summary>
/// Crafts the token pair (access + refresh) returned by authentication flows.
/// </summary>
public class TokenIssuer
{
    private readonly JwtTokenService _jwtTokenService;
    private readonly JwtOptions _jwtOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenIssuer"/> class.
    /// </summary>
    public TokenIssuer(
        JwtTokenService jwtTokenService,
        IOptions<JwtOptions> jwtOptions)
    {
        _jwtTokenService = jwtTokenService;
        _jwtOptions = jwtOptions.Value;
    }

    /// <summary>
    /// Builds a token pair for the specified user.
    /// </summary>
    /// <returns>The token response and the persistence-ready refresh token entity.</returns>
    public AuthResponseDto Issue(
        User user,
        out RefreshToken refreshToken)
    {
        var accessToken = _jwtTokenService.GenerateAccessToken(
            user.Id,
            user.Email,
            user.DisplayName);

        refreshToken = RefreshToken.Create(
            user.Id,
            _jwtTokenService.GenerateRefreshToken(),
            DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiryDays));

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            ExpiresIn = _jwtOptions.AccessTokenExpiryMinutes * 60
        };
    }
}