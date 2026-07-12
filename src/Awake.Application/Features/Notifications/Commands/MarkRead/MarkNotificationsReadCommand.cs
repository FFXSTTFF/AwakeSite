using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Notifications.Commands.MarkRead;

public record MarkNotificationsReadCommand : IRequest<Result<bool>>;
