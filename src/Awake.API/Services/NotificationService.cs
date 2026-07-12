using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Notifications.Dtos;
using Awake.API.Hubs;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using Microsoft.AspNetCore.SignalR;

namespace Awake.API.Services;

public class NotificationService(
    INotificationRepository notificationRepository,
    IUserRepository userRepository,
    IHubContext<NotificationHub> hubContext
) : INotificationService
{
    public async Task CreateAsync(Guid userId, string title, string body, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Body = body,
        };

        await notificationRepository.AddAsync(notification, ct);

        var dto = new NotificationDto(notification.Id, title, body, false, notification.CreatedAt);
        await hubContext.Clients.Group(userId.ToString())
            .SendAsync("Notification", dto, ct);
    }

    public async Task CreateForRankAsync(UserRank minRank, string title, string body, CancellationToken ct = default)
    {
        var users = await userRepository.GetByMinRankAsync(minRank, ct);
        foreach (var user in users)
            await CreateAsync(user.Id, title, body, ct);
    }
}
