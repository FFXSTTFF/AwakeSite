internal static class DiscordSlashCommands
{
    internal static object[] Build() =>
    [
        new
        {
            name = "ticket",
            type = 1,
            description = "Submit an application to join clan Awake [LOVE] (direct modal)"
        },
        new
        {
            name = "szticket",
            type = 1,
            description = "Post the application button message in this channel"
        },
        new
        {
            name = "szticketadm",
            type = 1,
            description = "Set this channel as admin ticket feed",
            options = new object[]
            {
                new { name = "role", description = "Officer/Admin role that gets access to ticket channels", type = 8, required = false }
            }
        },
        new
        {
            name = "loadout",
            type = 1,
            description = "Set your gear loadout for the recruitment application",
            options = new object[]
            {
                new { name = "weapon",      description = "Primary weapon",              type = 3, required = false, autocomplete = true },
                new { name = "weapon_rank", description = "Weapon upgrade level (0-15)", type = 4, required = false, min_value = 0, max_value = 15 },
                new { name = "armor",       description = "Armor",                       type = 3, required = false, autocomplete = true },
                new { name = "armor_rank",  description = "Armor upgrade level (0-15)",  type = 4, required = false, min_value = 0, max_value = 15 },
                new { name = "sniper",      description = "Sniper rifle (optional)",      type = 3, required = false, autocomplete = true },
                new { name = "sniper_rank", description = "Sniper upgrade level (0-15)", type = 4, required = false, min_value = 0, max_value = 15 }
            }
        }
    ];
}
