using System.Security.Claims;
using BuildingBlocks.Application;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Infrastructure.CurrentUser;

/// <summary>
/// Default <see cref="ICurrentUser"/> implementation backed by the HTTP request's
/// authenticated claims principal.
/// </summary>
public class CurrentUserService : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrentUserService"/> class.
    /// </summary>
    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public Guid? UserId
    {
        get
        {
            var value = GetClaim(ClaimTypes.NameIdentifier)
                ?? GetClaim("sub");
            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    /// <inheritdoc />
    public string? Email => GetClaim(ClaimTypes.Email)
        ?? GetClaim("email");

    /// <inheritdoc />
    public string? DisplayName => GetClaim(ClaimTypes.Name)
        ?? GetClaim("name");

    /// <inheritdoc />
    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    private string? GetClaim(string claimType)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindFirstValue(claimType);
    }
}
