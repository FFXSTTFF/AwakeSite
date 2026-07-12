using Awake.Domain.Common;
using Awake.Domain.Enums;

namespace Awake.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? Email { get; set; }
    public UserRank Rank { get; set; } = UserRank.Guest;
    public string? GameNickname { get; set; }

    // Discord OAuth — единственный способ входа; ключ связывания с заявками
    public string? DiscordUserId { get; set; }
    public string? DiscordUsername { get; set; }
    public string? DiscordAvatarUrl { get; set; }
    public ICollection<SquadMember> SquadMemberships { get; set; } = [];
    public ICollection<Ticket> Tickets { get; set; } = [];
}
