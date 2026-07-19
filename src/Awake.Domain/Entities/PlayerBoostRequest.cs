using Awake.Domain.Common;
using Awake.Domain.Enums;

namespace Awake.Domain.Entities;

public class PlayerBoostRequest : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public BoostType BoostType { get; set; }
    public string ItemId { get; set; } = "";
}
