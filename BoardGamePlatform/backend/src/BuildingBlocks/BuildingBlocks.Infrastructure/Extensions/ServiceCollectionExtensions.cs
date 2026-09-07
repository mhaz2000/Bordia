using System.Reflection;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Infrastructure.Caching;
using BuildingBlocks.Infrastructure.CurrentUser;
using BuildingBlocks.Infrastructure.HealthChecks;
using BuildingBlocks.Infrastructure.Messaging;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Security;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering shared infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers DbContext, MediatR, FluentValidation, AutoMapper, Redis, RabbitMQ,
    /// JWT, outbox, and health checks for a service.
    /// Call this from each service's Program.cs with the service's DbContext type.
    /// </summary>
    /// <typeparam name="TDbContext">The service-specific DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="assemblies">
    /// Optional list of assemblies to scan for MediatR handlers, validators, and AutoMapper profiles.
    /// If not provided, defaults to the assembly containing <typeparamref name="TDbContext"/>.
    /// </param>
    public static IServiceCollection AddAppInfrastructure<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] assemblies)
        where TDbContext : AppDbContext
    {
        var scanAssemblies = assemblies.Length > 0
            ? assemblies
            : [typeof(TDbContext).Assembly];

        // Register DbContext with PostgreSQL and snake_case naming
        services.AddDbContext<TDbContext>(options =>
            options
                .UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "public"))
                .UseSnakeCaseNamingConvention());

        // Register MediatR with pipeline behaviors
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(scanAssemblies);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // Register FluentValidation validators
        services.AddValidatorsFromAssemblies(scanAssemblies);

        // Register AutoMapper profiles (scanned from the same assemblies)
        services.AddAutoMapper(cfg =>
        {
            cfg.AddMaps(scanAssemblies);
        });

        // Register unit of work as a thin SaveChangesAsync wrapper over the DbContext
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<TDbContext>>();

        // Register the transactional outbox
        services.AddScoped<IOutbox, OutboxEfCore<TDbContext>>();
        services.AddHostedService<OutboxProcessor>();

        // Register Redis as the distributed cache + typed cache service
        var redisConnection = configuration.GetValue<string?>("Redis:Configuration")
            ?? configuration.GetConnectionString("Redis");
        services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
        services.AddSingleton<RedisCacheService>();

        // Register RabbitMQ publisher
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMQ"));
        services.AddScoped<IIntegrationEventPublisher, RabbitMqPublisher>();

        // Register JWT token service
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddSingleton<JwtTokenService>();

        // Register current user access
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentUser, CurrentUserService>();

        // Register infrastructure health checks (database, Redis, RabbitMQ)
        services.AddAppHealthChecks<TDbContext>();

        return services;
    }
}