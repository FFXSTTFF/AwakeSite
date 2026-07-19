using Awake.Application.Features.Boosts.Dtos;

namespace Awake.Application.Features.Boosts.Queries.GetBoostsSummary;

public record BoostSummaryEntryDto(
    Guid UserId,
    string Username,
    string? GameNickname,
    IReadOnlyList<BoostItemDto> Boosts);
