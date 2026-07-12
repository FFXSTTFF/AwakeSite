using Awake.Domain.Entities;

namespace Awake.Application.Common.Interfaces.Repositories;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}
