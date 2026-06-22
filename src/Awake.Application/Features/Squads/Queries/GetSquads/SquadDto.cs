namespace Awake.Application.Features.Squads.Queries.GetSquads;

public record SquadMemberDto(
    Guid UserId,
    string Username,
    string? GameNickname,
    bool IsLeader,
    DateTime JoinedAt);

public record SquadDto(
    Guid Id,
    string Name,
    int Number,
    IReadOnlyList<SquadMemberDto> Members,
    int MemberCount);
