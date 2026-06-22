namespace Awake.Infrastructure.ExternalServices.Discord;

public record DiscordWebhookPayload(DiscordEmbed[] Embeds);

public record DiscordEmbed(string Title, string Description, int Color);
