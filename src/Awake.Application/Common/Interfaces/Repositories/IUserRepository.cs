using Awake.Domain.Entities;
using Awake.Domain.Enums;

namespace Awake.Application.Common.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByDiscordUserIdAsync(string discordUserId, CancellationToken ct = default);
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetByMinRankAsync(UserRank minRank, CancellationToken ct = default);
}
