using Awake.Domain.Enums;
using Awake.Domain.ValueObjects;

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
    PlayerProfile? PlayerData,
    Loadout? Loadout
);
