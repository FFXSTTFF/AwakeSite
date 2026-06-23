using System.Net.Http.Json;
using System.Text.Json;
using Awake.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Awake.Infrastructure.ExternalServices.Discord;

public class DiscordBotService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<DiscordBotService> logger) : IDiscordBotService
{
    private const string DiscordApiBase = "https://discord.com/api/v10";

    public async Task SendDmAsync(string discordUserId, string message, CancellationToken ct = default)
    {
        var botToken = configuration["Discord:BotToken"];
        if (string.IsNullOrWhiteSpace(botToken))
        {
            logger.LogWarning("Discord:BotToken is not configured. Skipping DM.");
            return;
        }

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bot {botToken}");

        try
        {
            // 1. Open DM channel
            var channelResponse = await httpClient.PostAsJsonAsync(
                $"{DiscordApiBase}/users/@me/channels",
                new { recipient_id = discordUserId },
                ct);

            if (!channelResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to open DM channel for user {UserId}: {Status}",
                    discordUserId, channelResponse.StatusCode);
                return;
            }

            var channelDoc = await channelResponse.Content.ReadFromJsonAsync<JsonDocument>(ct);
            var channelId = channelDoc?.RootElement.GetProperty("id").GetString();
            if (string.IsNullOrEmpty(channelId))
                return;

            // 2. Send message
            var msgResponse = await httpClient.PostAsJsonAsync(
                $"{DiscordApiBase}/channels/{channelId}/messages",
                new { content = message },
                ct);

            if (!msgResponse.IsSuccessStatusCode)
                logger.LogWarning("Failed to send DM to Discord user {UserId}: {Status}",
                    discordUserId, msgResponse.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception while sending Discord DM to {UserId}.", discordUserId);
        }
    }
}
