using BuildingBlocks.Infrastructure.Messaging;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace BuildingBlocks.Infrastructure.HealthChecks;

/// <summary>
/// Extension methods for registering infrastructure health checks.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Registers health checks for the service's <typeparamref name="TDbContext"/>,
    /// Redis (via <see cref="IDistributedCache"/>), and RabbitMQ.
    /// </summary>
    /// <typeparam name="TDbContext">The service's DbContext type.</typeparam>
    public static IHealthChecksBuilder AddAppHealthChecks<TDbContext>(this IServiceCollection services)
        where TDbContext : AppDbContext
    {
        var builder = services.AddHealthChecks();

        builder.AddDbContextCheck<TDbContext>("database");
        builder.AddCheck<RedisHealthCheck>("redis");
        builder.AddCheck<RabbitMqHealthCheck>("rabbitmq");

        return builder;
    }

    /// <summary>
    /// Maps the standard <c>/health</c> and <c>/health/ready</c> endpoints.
    /// </summary>
    public static void MapAppHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/ready");
    }
}

/// <summary>
/// Health check that verifies the Redis connection by attempting a cache read.
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IDistributedCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisHealthCheck"/> class.
    /// </summary>
    public RedisHealthCheck(IDistributedCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.GetAsync("healthcheck", cancellationToken);
            return HealthCheckResult.Healthy("Redis connection is available.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis connection failed.", ex);
        }
    }
}

/// <summary>
/// Health check that verifies a RabbitMQ connection can be established.
/// </summary>
public class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqHealthCheck"/> class.
    /// </summary>
    public RabbitMqHealthCheck(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(3),
                SocketReadTimeout = TimeSpan.FromSeconds(3),
                SocketWriteTimeout = TimeSpan.FromSeconds(3),
                AutomaticRecoveryEnabled = false
            };

            using var connection = factory.CreateConnection();
            return Task.FromResult(
                connection.IsOpen
                    ? HealthCheckResult.Healthy("RabbitMQ connection is available.")
                    : HealthCheckResult.Unhealthy("RabbitMQ connection is closed."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ connection failed.", ex));
        }
    }
}