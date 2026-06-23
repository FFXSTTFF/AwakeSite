using Awake.Domain.Entities;

namespace Awake.Application.Common.Interfaces.Repositories;

public interface IDiscordGuildSettingsRepository
{
    Task<DiscordGuildSettings?> GetByGuildIdAsync(string guildId, CancellationToken ct = default);
    Task UpsertAsync(DiscordGuildSettings settings, CancellationToken ct = default);
}
