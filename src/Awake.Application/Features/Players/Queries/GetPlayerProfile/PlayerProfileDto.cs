using Awake.Domain.Enums;
using Awake.Domain.ValueObjects;

namespace Awake.Application.Features.Players.Queries.GetPlayerProfile;

public record PlayerSquadDto(Guid Id, string Name, int Number, bool IsLeader);

public record PlayerStatsDto(
    int Kills, int Deaths, double KdRatio,
    string Accuracy, string Playtime,
    IReadOnlyList<ClanEntry> ClanHistory,
    DateTime FetchedAt);

public record PlayerProfileDto(
    Guid UserId,
    string Username,
    string? DiscordUsername,
    string? DiscordAvatarUrl,
    UserRank Rank,
    string? GameNickname,
    PlayerSquadDto? Squad,
    PlayerStatsDto? Stats,
    Loadout? Loadout);
