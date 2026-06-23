using Awake.Domain.Enums;

namespace Awake.Application.Features.Tickets.Dtos;

public record TicketDetailDto(
    Guid Id,
    TicketType Type,
    TicketStatus Status,
    string GameNickname,
    string AuthorUsername,
    string Description,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewedByUsername,
    IReadOnlyList<TicketCommentDto> Comments,
    object? PlayerData
);
