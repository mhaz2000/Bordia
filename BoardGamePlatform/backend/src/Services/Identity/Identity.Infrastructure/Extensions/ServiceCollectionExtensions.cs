using BuildingBlocks.Infrastructure.Extensions;
using BuildingBlocks.Infrastructure.Security;
using Identity.Application.Common;
using Identity.Application.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Extensions;

/// <summary>
/// DI registration for the Identity service.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Identity DbContext, MediatR pipeline, hasher, and JWT authentication.
    /// </summary>
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAppInfrastructure<IdentityDbContext>(configuration);

        services.AddScoped<IPasswordHasher, PasswordHasher>();

        services.AddScoped<TokenIssuer>();

        services.AddAppJwtAuthentication(configuration);

        return services;
    }
}