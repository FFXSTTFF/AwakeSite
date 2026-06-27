namespace Awake.Domain.ValueObjects;

public record PlayerProfile(
    int Kills,
    int Deaths,
    double KdRatio,
    string Accuracy,
    string Playtime,
    IReadOnlyList<ClanEntry> ClanHistory
);
