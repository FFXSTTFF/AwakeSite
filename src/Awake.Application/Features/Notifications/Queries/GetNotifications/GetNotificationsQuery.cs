using Awake.Application.Common.Models;
using Awake.Application.Features.Notifications.Dtos;
using MediatR;

namespace Awake.Application.Features.Notifications.Queries.GetNotifications;

public record GetNotificationsQuery : IRequest<Result<IReadOnlyList<NotificationDto>>>;
