using Awake.Domain.Common;

namespace Awake.Domain.Entities;

public class PlayerInventoryItem : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    /// <summary>ID предмета из stalzone-database (например "1r79g").</summary>
    public string ItemId { get; set; } = string.Empty;
}
