namespace Awake.Application.Features.Tickets.Dtos;

public record TicketCommentDto(
    Guid Id,
    string AuthorUsername,
    string Content,
    DateTime CreatedAt
);
