using Awake.Domain.Common;

namespace Awake.Domain.Entities;

public class Squad : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int Number { get; set; }
    public ICollection<SquadMember> Members { get; set; } = [];
}
