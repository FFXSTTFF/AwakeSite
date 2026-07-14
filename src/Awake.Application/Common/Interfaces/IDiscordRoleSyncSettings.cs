using Awake.Domain.Enums;

namespace Awake.Application.Common.Interfaces;

/// <summary>
/// Настройки синка рангов с ролями Discord (Discord — источник правды).
/// Маппинг никогда не содержит Leader: владение платформой не управляется ролями Discord.
/// </summary>
public interface IDiscordRoleSyncSettings
{
    bool Enabled { get; }
    string? GuildId { get; }
    IReadOnlyDictionary<string, UserRank> RoleToRank { get; }
}
