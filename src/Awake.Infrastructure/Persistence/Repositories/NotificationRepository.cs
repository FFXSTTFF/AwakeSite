using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Awake.Infrastructure.Persistence.Repositories;

public class NotificationRepository(AppDbContext context) : INotificationRepository
{
    public async Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        await context.Notifications.AddAsync(notification, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Notification>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        await context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }
}
