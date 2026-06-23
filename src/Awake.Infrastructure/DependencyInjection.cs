using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Infrastructure.ExternalServices.Discord;
using Awake.Infrastructure.ExternalServices.PlayerData;
using Awake.Infrastructure.ExternalServices.PlayerData.Sources;
using Awake.Infrastructure.Identity;
using Awake.Infrastructure.Persistence;
using Awake.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Awake.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(configuration.GetConnectionString("Postgres")));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISquadRepository, SquadRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Discord
        services.AddHttpClient<IDiscordNotifier, DiscordNotifier>();
        services.AddHttpClient<IDiscordBotService, DiscordBotService>();

        // Player data sources
        services.AddScoped<IPlayerDataSource, StubDataSource>();
        services.AddScoped<IPlayerDataAggregator, PlayerDataAggregator>();

        // Identity services
        services.AddScoped<IPasswordHasher, PasswordHasherService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddHttpContextAccessor();

        return services;
    }
}
