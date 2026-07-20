using Awake.Application.Features.Boosts.Dtos;
using Awake.Application.Features.Inventory.Dtos;

namespace Awake.Application.Features.Squads.Queries.GetSquadReserve;

public record ReserveMemberDto(
    Guid UserId,
    string Username,
    string? GameNickname,
    PlayerFlagsDto Flags,
    double? Kd,
    IReadOnlyList<BoostItemDto> Boosts);
