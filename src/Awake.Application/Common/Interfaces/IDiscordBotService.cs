namespace Awake.Application.Common.Interfaces;

public interface IDiscordBotService
{
    Task SendDmAsync(string discordUserId, string message, CancellationToken ct = default);
}
