using Awake.Domain.Enums;

namespace Awake.Application.Features.Tickets.Dtos;

public record TicketListItemDto(
    Guid Id,
    TicketType Type,
    TicketStatus Status,
    string GameNickname,
    string AuthorUsername,
    DateTime CreatedAt
);
