using Awake.Application.Features.Boosts.Dtos;
using Awake.Application.Features.Inventory.Dtos;

namespace Awake.Application.Features.Squads.Queries.GetSquads;

public record SquadMemberDto(
    Guid UserId,
    string Username,
    string? GameNickname,
    bool IsLeader,
    DateTime JoinedAt,
    PlayerFlagsDto Flags,
    double? Kd,
    IReadOnlyList<BoostItemDto> Boosts);

public record SquadDto(
    Guid Id,
    string Name,
    int Number,
    IReadOnlyList<SquadMemberDto> Members,
    int MemberCount);
