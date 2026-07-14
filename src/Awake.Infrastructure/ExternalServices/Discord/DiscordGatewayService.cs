using Awake.Application.Features.Auth.Commands.SyncDiscordRoles;
using Awake.Application.Features.Tickets.Commands.AddTicketComment;
using Discord;
using Discord.WebSocket;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Awake.Infrastructure.ExternalServices.Discord;

public class DiscordGatewayService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _services;
    private readonly ILogger<DiscordGatewayService> _logger;

    public DiscordGatewayService(
        IConfiguration configuration,
        IServiceProvider services,
        ILogger<DiscordGatewayService> logger)
    {
        _configuration = configuration;
        _services = services;
        _logger = logger;

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            // GuildMembers — привилегированный интент: должен быть включён
            // в Discord Developer Portal (Bot → Server Members Intent)
            GatewayIntents = GatewayIntents.Guilds
                           | GatewayIntents.GuildMessages
                           | GatewayIntents.MessageContent
                           | GatewayIntents.GuildMembers,
            LogLevel = LogSeverity.Warning
        });

        _client.Log += OnLog;
        _client.MessageReceived += OnMessageReceived;
        _client.GuildMemberUpdated += OnGuildMemberUpdated;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = _configuration["Discord:BotToken"];
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogInformation("Discord:BotToken not configured — Gateway not started.");
            return;
        }

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        _logger.LogInformation("Discord Gateway connected.");

        await Task.Delay(Timeout.Infinite, stoppingToken);

        await _client.StopAsync();
    }

    private async Task OnMessageReceived(SocketMessage message)
    {
        // Ignore bot messages (including our own)
        if (message.Author.IsBot) return;
        if (message is not SocketUserMessage userMessage) return;

        var channelId = userMessage.Channel.Id.ToString();

        await using var scope = _services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var ticketRepo = scope.ServiceProvider
            .GetRequiredService<Awake.Application.Common.Interfaces.Repositories.ITicketRepository>();

        var ticket = await ticketRepo.GetByDiscordChannelIdAsync(channelId);
        if (ticket is null) return;

        var authorName = (message.Author as SocketGuildUser)?.DisplayName
                         ?? message.Author.GlobalName
                         ?? message.Author.Username;

        var content = userMessage.Content;
        if (string.IsNullOrWhiteSpace(content)) return;

        var result = await mediator.Send(new AddDiscordCommentCommand(ticket.Id, authorName, content));
        if (!result.IsSuccess)
            _logger.LogWarning("Failed to save Discord comment for ticket {TicketId}: {Error}",
                ticket.Id, result.Error);
    }

    private async Task OnGuildMemberUpdated(
        Cacheable<SocketGuildUser, ulong> before, SocketGuildUser after)
    {
        var guildId = _configuration["Discord:GuildId"];
        if (!string.IsNullOrWhiteSpace(guildId) && after.Guild.Id.ToString() != guildId) return;

        // Событие приходит и на смену ника/аватара — если роли не менялись, БД не дёргаем
        if (before.HasValue)
        {
            var oldRoles = before.Value.Roles.Select(r => r.Id).ToHashSet();
            if (oldRoles.SetEquals(after.Roles.Select(r => r.Id))) return;
        }

        var roleIds = after.Roles
            .Where(r => !r.IsEveryone)
            .Select(r => r.Id.ToString())
            .ToList();

        await using var scope = _services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new SyncDiscordRolesCommand(after.Id.ToString(), roleIds));
        if (!result.IsSuccess)
            _logger.LogWarning("Discord role sync failed for {UserId}: {Error}", after.Id, result.Error);
    }

    private Task OnLog(LogMessage log)
    {
        _logger.Log(log.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error    => LogLevel.Error,
            LogSeverity.Warning  => LogLevel.Warning,
            _                    => LogLevel.Debug
        }, log.Exception, "[Discord.Net] {Message}", log.Message);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _client.Dispose();
        base.Dispose();
    }
}
