using Awake.Domain.Common;
using Awake.Domain.Enums;

namespace Awake.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Email { get; set; }
    public UserRank Rank { get; set; } = UserRank.Guest;
    public string? GameNickname { get; set; }
    public ICollection<SquadMember> SquadMemberships { get; set; } = [];
    public ICollection<Ticket> Tickets { get; set; } = [];
}
