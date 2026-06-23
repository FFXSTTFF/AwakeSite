namespace Awake.Application.Features.Notifications.Dtos;

public record NotificationDto(
    Guid Id,
    string Title,
    string Body,
    bool IsRead,
    DateTime CreatedAt
);
