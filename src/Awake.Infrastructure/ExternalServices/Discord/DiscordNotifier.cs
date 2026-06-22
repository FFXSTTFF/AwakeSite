using System.Net.Http.Json;
using Awake.Application.Common.Interfaces;
using Awake.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Awake.Infrastructure.ExternalServices.Discord;

public class DiscordNotifier(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<DiscordNotifier> logger) : IDiscordNotifier
{
    // #3ddc84 as decimal
    private const int AccentColor = 4054396;

    public async Task NotifyNewTicketAsync(Ticket ticket, CancellationToken ct = default)
    {
        var webhookUrl = configuration["Discord:WebhookUrl"];
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            logger.LogWarning("Discord:WebhookUrl is not configured. Skipping notification.");
            return;
        }

        var payload = new DiscordWebhookPayload(
        [
            new DiscordEmbed(
                Title: "Новая заявка",
                Description: $"Никнейм: {ticket.GameNickname}, Тип: {ticket.Type}",
                Color: AccentColor)
        ]);

        await PostSafeAsync(webhookUrl, payload, ct);
    }

    public async Task NotifyTicketDecisionAsync(Ticket ticket, CancellationToken ct = default)
    {
        var webhookUrl = configuration["Discord:WebhookUrl"];
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            logger.LogWarning("Discord:WebhookUrl is not configured. Skipping notification.");
            return;
        }

        var payload = new DiscordWebhookPayload(
        [
            new DiscordEmbed(
                Title: "Решение по заявке",
                Description: $"Никнейм: {ticket.GameNickname}, Статус: {ticket.Status}, Рассмотрел: {ticket.ReviewedBy}",
                Color: AccentColor)
        ]);

        await PostSafeAsync(webhookUrl, payload, ct);
    }

    private async Task PostSafeAsync(string url, DiscordWebhookPayload payload, CancellationToken ct)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(url, payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Discord webhook returned non-success status code {StatusCode}.",
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Discord webhook notification.");
        }
    }
}
