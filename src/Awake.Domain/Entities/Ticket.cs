using Awake.Domain.Common;
using Awake.Domain.Enums;

namespace Awake.Domain.Entities;

public class Ticket : BaseEntity
{
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public string GameNickname { get; set; } = string.Empty;
    public TicketType Type { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Pending;
    public string Description { get; set; } = string.Empty;
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public ICollection<TicketComment> Comments { get; set; } = [];
}
