using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Entities;
using Awake.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Awake.Infrastructure.Persistence.Repositories;

public class PlayerStatsSnapshotRepository(AppDbContext context) : IPlayerStatsSnapshotRepository
{
    public async Task<PlayerStatsSnapshot?> GetByNicknameAsync(string gameNickname, CancellationToken ct = default)
        => await context.PlayerStatsSnapshots
            .FirstOrDefaultAsync(s => s.GameNickname == gameNickname, ct);

    public async Task<IReadOnlyList<PlayerStatsSnapshot>> GetByNicknamesAsync(
        IReadOnlyCollection<string> gameNicknames, CancellationToken ct = default)
        => await context.PlayerStatsSnapshots
            .Where(s => gameNicknames.Contains(s.GameNickname))
            .ToListAsync(ct);

    public async Task UpsertAsync(string gameNickname, PlayerProfile profile, CancellationToken ct = default)
    {
        try
        {
            await UpsertCoreAsync(gameNickname, profile, ct);
        }
        catch (DbUpdateException)
        {
            // Гонка на unique-индексе: параллельная вставка того же ника победила.
            // Сбрасываем трекер и повторяем один раз — теперь строка точно есть → update.
            context.ChangeTracker.Clear();
            await UpsertCoreAsync(gameNickname, profile, ct);
        }
    }

    private async Task UpsertCoreAsync(string gameNickname, PlayerProfile profile, CancellationToken ct)
    {
        var existing = await context.PlayerStatsSnapshots
            .FirstOrDefaultAsync(s => s.GameNickname == gameNickname, ct);

        if (existing is null)
        {
            existing = new PlayerStatsSnapshot { GameNickname = gameNickname };
            context.PlayerStatsSnapshots.Add(existing);
        }

        existing.Kills = profile.Kills;
        existing.Deaths = profile.Deaths;
        existing.KdRatio = profile.KdRatio;
        existing.Accuracy = profile.Accuracy;
        existing.Playtime = profile.Playtime;
        existing.ClanHistory = profile.ClanHistory.ToList();
        existing.FetchedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);
    }
}
