using BuildingBlocks.Infrastructure.Extensions;
using BuildingBlocks.Infrastructure.Security;
using Game.Application.Common;
using Game.Application.Persistence;
using Game.Infrastructure.Realtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UNO;

namespace Game.Infrastructure.Extensions;

/// <summary>
/// DI registration for the Game service.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Game DbContext, MediatR pipeline, real-time push, game engine, and JWT authentication.
    /// </summary>
    public static IServiceCollection AddGameInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAppInfrastructure<GameDbContext>(configuration);

        services.AddAppJwtAuthentication(configuration);

        // Game engine access by game type.
        services.AddSingleton<IGameEngineProvider, GameEngineProvider>();

        // Game implementations. Plugin-style runtime loading is deferred; games are
        // registered directly here until a real multi-game need emerges.
        services.AddSingleton<GameEngine.Core.IGame, UNO.UNOGame>();

        // Real-time notifier translating Application game-changed notifications into hub calls.
        services.AddScoped<MediatR.INotificationHandler<Game.Application.Realtime.GameStateChanged>, GameRealTimeNotifier>();

        // Enforces per-game turn timers by polling session deadlines every second.
        services.AddHostedService<Services.TurnTimeoutService>();

        return services;
    }
}