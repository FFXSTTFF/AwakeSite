namespace Awake.Application.Features.Public.Queries.GetLeaderboard;

public record LeaderboardEntryDto(
    string GameNickname,
    int Kills,
    string Accuracy,
    string Playtime);
