using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Notifications.Commands.MarkRead;

public class MarkNotificationsReadCommandHandler(
    INotificationRepository notificationRepository,
    ICurrentUserService currentUserService
) : IRequestHandler<MarkNotificationsReadCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        MarkNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        await notificationRepository.MarkAllReadAsync(currentUserService.UserId, cancellationToken);
        return Result<bool>.Success(true);
    }
}
