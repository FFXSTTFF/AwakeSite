using Awake.Domain.Common;
using Awake.Domain.Enums;

namespace Awake.Domain.Entities;

public class PlayerBuildProof : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public BuildType BuildType { get; set; }
    public byte[] Image { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
}
