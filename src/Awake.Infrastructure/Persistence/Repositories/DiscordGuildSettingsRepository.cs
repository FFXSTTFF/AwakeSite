using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Awake.Infrastructure.Persistence.Repositories;

public class DiscordGuildSettingsRepository(AppDbContext context) : IDiscordGuildSettingsRepository
{
    public async Task<DiscordGuildSettings?> GetByGuildIdAsync(string guildId, CancellationToken ct = default)
        => await context.DiscordGuildSettings.FindAsync([guildId], ct);

    public async Task UpsertAsync(DiscordGuildSettings settings, CancellationToken ct = default)
    {
        var existing = await context.DiscordGuildSettings.FindAsync([settings.GuildId], ct);
        if (existing is null)
        {
            await context.DiscordGuildSettings.AddAsync(settings, ct);
        }
        else
        {
            if (settings.AdminChannelId is not null) existing.AdminChannelId = settings.AdminChannelId;
            if (settings.AdminRoleId is not null) existing.AdminRoleId = settings.AdminRoleId;
            if (settings.TicketCategoryId is not null) existing.TicketCategoryId = settings.TicketCategoryId;
        }
        await context.SaveChangesAsync(ct);
    }
}
