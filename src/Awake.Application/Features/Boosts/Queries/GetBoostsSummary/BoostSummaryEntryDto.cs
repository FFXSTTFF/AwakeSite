using Awake.Domain.Enums;

namespace Awake.Application.Features.Boosts.Queries.GetBoostsSummary;

public record BoostSummaryEntryDto(
    Guid UserId,
    string Username,
    string? GameNickname,
    IReadOnlyList<BoostType> BoostTypes);
