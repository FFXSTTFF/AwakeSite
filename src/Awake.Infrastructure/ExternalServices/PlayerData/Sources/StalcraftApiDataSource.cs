using System.Net.Http.Json;
using System.Text.Json;
using Awake.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Awake.Infrastructure.ExternalServices.PlayerData.Sources;

public sealed class StalcraftApiDataSource(
    IHttpClientFactory factory,
    ILogger<StalcraftApiDataSource> logger)
    : IPlayerDataSource
{
    private const string Region = "eu";

    public async Task<PlayerProfile?> TryGetDataAsync(string nickname, CancellationToken ct = default)
    {
        logger.LogInformation("Fetching player profile via STALCRAFT API for {Nickname}", nickname);
        try
        {
            var http = factory.CreateClient("stalcraftapi");
            var url = $"{Region}/character/by-name/{Uri.EscapeDataString(nickname)}/profile";
            var resp = await http.GetAsync(url, ct);

            logger.LogInformation("STALCRAFT API response: {Status}", (int)resp.StatusCode);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
            return json is { } j ? ParseProfile(j) : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "STALCRAFT API failed for {Nickname}", nickname);
            return null;
        }
    }

    internal static PlayerProfile? ParseProfile(JsonElement json)
    {
        var statMap = new Dictionary<string, double>();
        if (json.TryGetProperty("stats", out var statsEl))
        {
            foreach (var stat in statsEl.EnumerateArray())
            {
                var id = stat.GetProperty("id").GetString() ?? "";
                var val = stat.GetProperty("value");
                if (val.ValueKind is JsonValueKind.Number)
                    statMap[id] = val.GetDouble();
            }
        }

        var kills  = (int)statMap.GetValueOrDefault("kil");
        var deaths = (int)statMap.GetValueOrDefault("dea");

        if (kills == 0 && deaths == 0) return null;

        var kd = deaths > 0 ? Math.Round(kills / (double)deaths, 2) : (double)kills;

        var shotsFired = statMap.GetValueOrDefault("sho-fir");
        var shotsHit   = statMap.GetValueOrDefault("sho-hit");
        var accuracy   = shotsFired > 0
            ? $"{Math.Round(shotsHit * 100.0 / shotsFired, 1)}%"
            : "—";

        var playtimeMs = statMap.GetValueOrDefault("pla-tim");
        var playtime   = FormatDurationMs(playtimeMs);

        var clanHistory = new List<ClanEntry>();
        if (json.TryGetProperty("clan", out var clanEl) &&
            clanEl.ValueKind == JsonValueKind.Object &&
            clanEl.TryGetProperty("info", out var infoEl))
        {
            var name = infoEl.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var tag  = infoEl.TryGetProperty("tag",  out var t) ? t.GetString() ?? "" : "";
            if (!string.IsNullOrEmpty(name))
                clanHistory.Add(new ClanEntry(name, tag, ""));
        }

        return new PlayerProfile(kills, deaths, kd, accuracy, playtime, clanHistory);
    }

    private static string FormatDurationMs(double ms)
    {
        if (ms <= 0) return "—";
        var totalHours = (long)(ms / 3_600_000);
        var days  = totalHours / 24;
        var hours = totalHours % 24;
        return days > 0 ? $"{days}d {hours}h" : $"{hours}h";
    }
}
