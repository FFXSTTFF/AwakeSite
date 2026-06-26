using Awake.Domain.Common;
using Awake.Domain.Enums;
using Awake.Domain.ValueObjects;

namespace Awake.Domain.Entities;

public class Ticket : BaseEntity
{
    // Nullable — Discord-submitted tickets have no website AuthorId
    public Guid? AuthorId { get; set; }
    public User? Author { get; set; }

    // Populated when ticket is submitted via Discord bot
    public string? DiscordUserId { get; set; }
    public string? DiscordUsername { get; set; }

    public string GameNickname { get; set; } = string.Empty;
    public TicketType Type { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Pending;
    public string Description { get; set; } = string.Empty;
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public ICollection<TicketComment> Comments { get; set; } = [];

    // Private Discord channel created for this ticket
    public string? DiscordChannelId { get; set; }

    public Loadout? Loadout { get; set; }
}
