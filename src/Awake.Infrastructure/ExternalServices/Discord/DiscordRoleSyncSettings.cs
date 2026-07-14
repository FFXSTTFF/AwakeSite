using Awake.Application.Common.Interfaces;
using Awake.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Awake.Infrastructure.ExternalServices.Discord;

/// <summary>
/// Конфиг-секция Discord:RoleSync:
///   "Enabled": true,
///   "RoleToRank": { "&lt;discord role id&gt;": "Member" | "Officer" | "Colonel" }
/// GuildId берётся из Discord:GuildId. Leader в маппинге игнорируется.
/// </summary>
public class DiscordRoleSyncSettings : IDiscordRoleSyncSettings
{
    public bool Enabled { get; }
    public string? GuildId { get; }
    public IReadOnlyDictionary<string, UserRank> RoleToRank { get; }

    public DiscordRoleSyncSettings(IConfiguration configuration, ILogger<DiscordRoleSyncSettings> logger)
    {
        Enabled = bool.TryParse(configuration["Discord:RoleSync:Enabled"], out var enabled) && enabled;
        GuildId = configuration["Discord:GuildId"];

        var map = new Dictionary<string, UserRank>();
        foreach (var child in configuration.GetSection("Discord:RoleSync:RoleToRank").GetChildren())
        {
            if (Enum.TryParse<UserRank>(child.Value, ignoreCase: true, out var rank)
                && rank is > UserRank.Guest and < UserRank.Leader)
            {
                map[child.Key] = rank;
            }
            else
            {
                logger.LogWarning(
                    "Discord:RoleSync — пропущен маппинг роли {RoleId}: значение '{Value}' " +
                    "(допустимы Member/Officer/Colonel)", child.Key, child.Value);
            }
        }
        RoleToRank = map;

        if (Enabled && string.IsNullOrWhiteSpace(GuildId))
            logger.LogWarning("Discord:RoleSync включён, но Discord:GuildId пуст — сверка при логине работать не будет.");
    }
}
