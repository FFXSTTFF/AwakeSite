using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Application.Features.Notifications.Dtos;
using MediatR;

namespace Awake.Application.Features.Notifications.Queries.GetNotifications;

public class GetNotificationsQueryHandler(
    INotificationRepository notificationRepository,
    ICurrentUserService currentUserService
) : IRequestHandler<GetNotificationsQuery, Result<IReadOnlyList<NotificationDto>>>
{
    public async Task<Result<IReadOnlyList<NotificationDto>>> Handle(
        GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var notifications = await notificationRepository.GetByUserIdAsync(
            currentUserService.UserId, cancellationToken);

        var dtos = notifications
            .Select(n => new NotificationDto(n.Id, n.Title, n.Body, n.IsRead, n.CreatedAt))
            .ToList();

        return Result<IReadOnlyList<NotificationDto>>.Success(dtos);
    }
}
