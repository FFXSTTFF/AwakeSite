using Awake.Domain.Enums;

namespace Awake.Application.Common.Interfaces;

public interface INotificationService
{
    Task CreateAsync(Guid userId, string title, string body, CancellationToken ct = default);
    Task CreateForRankAsync(UserRank minRank, string title, string body, CancellationToken ct = default);
}
