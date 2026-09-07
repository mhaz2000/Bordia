using BuildingBlocks.Infrastructure.Extensions;
using BuildingBlocks.Infrastructure.Security;
using Lobby.Application.Common;
using Lobby.Application.Persistence;
using Lobby.Infrastructure.Clients;
using Lobby.Infrastructure.Realtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lobby.Infrastructure.Extensions;

/// <summary>
/// DI registration for the Lobby service.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Lobby DbContext, MediatR pipeline, real-time push, and JWT authentication.
    /// </summary>
    public static IServiceCollection AddLobbyInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAppInfrastructure<LobbyDbContext>(configuration);

        services.AddAppJwtAuthentication(configuration);

        // Real-time notifier translating Application room-changed notifications into hub calls.
        services.AddScoped<MediatR.INotificationHandler<Lobby.Application.Realtime.LobbyRoomChanged>, LobbyRealTimeNotifier>();

        // Closes abandoned waiting rooms and purges long-closed ones.
        services.Configure<Services.RoomCleanupOptions>(configuration.GetSection("Lobby:RoomCleanup"));
        services.AddHostedService<Services.AbandonedRoomCleanupService>();

        // HTTP client for creating game sessions in the Game service.
        services.Configure<GameServiceOptions>(configuration.GetSection("Services:Game"));
        services.AddHttpClient<IGameSessionClient, GameSessionClient>();

        return services;
    }
}