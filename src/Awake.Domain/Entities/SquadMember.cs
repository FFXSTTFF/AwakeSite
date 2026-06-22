using Awake.Domain.Common;

namespace Awake.Domain.Entities;

public class SquadMember : BaseEntity
{
    public Guid SquadId { get; set; }
    public Squad Squad { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public bool IsLeader { get; set; } = false;
    public DateTime JoinedAt { get; init; } = DateTime.UtcNow;
}
