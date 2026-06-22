using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Awake.Infrastructure.Persistence.Repositories;

public class TicketRepository(AppDbContext context) : ITicketRepository
{
    public async Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Tickets.FindAsync([id], ct);

    public async Task<IReadOnlyList<Ticket>> GetAllAsync(CancellationToken ct = default)
        => await context.Tickets.ToListAsync(ct);

    public async Task<IReadOnlyList<Ticket>> GetByAuthorAsync(Guid authorId, CancellationToken ct = default)
        => await context.Tickets.Where(t => t.AuthorId == authorId).ToListAsync(ct);

    public async Task AddAsync(Ticket ticket, CancellationToken ct = default)
    {
        await context.Tickets.AddAsync(ticket, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Ticket ticket, CancellationToken ct = default)
    {
        context.Tickets.Update(ticket);
        await context.SaveChangesAsync(ct);
    }

    public async Task AddCommentAsync(TicketComment comment, CancellationToken ct = default)
    {
        await context.TicketComments.AddAsync(comment, ct);
        await context.SaveChangesAsync(ct);
    }
}
