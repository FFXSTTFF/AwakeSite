namespace Awake.Domain.Entities;

public class DiscordGuildSettings
{
    public string GuildId { get; set; } = string.Empty;
    public string? AdminChannelId { get; set; }
    public string? AdminRoleId { get; set; }
    public string? TicketCategoryId { get; set; }
}
