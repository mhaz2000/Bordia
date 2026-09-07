using Microsoft.Extensions.Hosting;
using Serilog;

namespace BuildingBlocks.Infrastructure.Logging;

/// <summary>
/// Extension methods for configuring Serilog.
/// </summary>
public static class SerilogExtensions
{
    /// <summary>
    /// Configures Serilog with console and structured logging on the host builder.
    /// </summary>
    /// <param name="hostBuilder">The host builder.</param>
    /// <param name="serviceName">The name of the service, included in log output.</param>
    public static IHostBuilder AddSerilogLogging(
        this IHostBuilder hostBuilder,
        string serviceName)
    {
        hostBuilder.UseSerilog((context, services, configuration) =>
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", serviceName)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}"));

        return hostBuilder;
    }
}
