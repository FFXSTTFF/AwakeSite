using Awake.Domain.Entities;

namespace Awake.Application.Common.Interfaces.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Ticket?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Ticket>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Ticket>> GetByAuthorAsync(Guid authorId, CancellationToken ct = default);
    Task AddAsync(Ticket ticket, CancellationToken ct = default);
    Task UpdateAsync(Ticket ticket, CancellationToken ct = default);
    Task AddCommentAsync(TicketComment comment, CancellationToken ct = default);
    Task<Ticket?> GetByDiscordChannelIdAsync(string channelId, CancellationToken ct = default);
    Task<Ticket?> GetOpenByDiscordUserIdAsync(string discordUserId, CancellationToken ct = default);
    Task<IReadOnlyList<Ticket>> GetUnlinkedByDiscordUserIdAsync(string discordUserId, CancellationToken ct = default);
}
