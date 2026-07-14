using Awake.Application.Features.Inventory.Dtos;

namespace Awake.Application.Features.Squads.Queries.GetSquadBuilder;

public record BuilderFighterDto(
    Guid UserId,
    string Username,
    string? GameNickname,
    string? AvatarUrl,
    PlayerFlagsDto Flags,
    double? Kd);

public record BuilderSquadDto(
    Guid Id,
    string Name,
    int Number,
    IReadOnlyList<BuilderFighterDto> Members);

public record SquadBuilderDto(
    IReadOnlyList<BuilderSquadDto> Squads,
    IReadOnlyList<BuilderFighterDto> Pool);
