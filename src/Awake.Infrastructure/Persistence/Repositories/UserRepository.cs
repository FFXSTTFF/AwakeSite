using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Awake.Infrastructure.Persistence.Repositories;

public class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Users.FindAsync([id], ct);

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => await context.Users.FirstOrDefaultAsync(u => u.Username == username, ct);

    public async Task<User?> GetByDiscordUserIdAsync(string discordUserId, CancellationToken ct = default)
        => await context.Users.FirstOrDefaultAsync(u => u.DiscordUserId == discordUserId, ct);

    public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default)
        => await context.Users.AnyAsync(u => u.Username == username, ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        await context.Users.AddAsync(user, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        context.Users.Update(user);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
        => await context.Users.ToListAsync(ct);

    public async Task<IReadOnlyList<User>> GetByMinRankAsync(UserRank minRank, CancellationToken ct = default)
        => await context.Users.Where(u => u.Rank >= minRank).ToListAsync(ct);
}
