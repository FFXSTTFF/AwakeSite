using System.Text.RegularExpressions;
using Awake.Domain.ValueObjects;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Awake.Infrastructure.ExternalServices.PlayerData.Sources;

public class StalcraftHqDataSource(
    IHttpClientFactory httpClientFactory,
    ILogger<StalcraftHqDataSource> logger) : IPlayerDataSource
{
    private const string Server = "EU";

    public async Task<PlayerProfile?> TryGetDataAsync(string nickname, CancellationToken ct = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient("stalcrafthq");
            var response = await client.GetAsync(
                $"/characters/{Server}/{Uri.EscapeDataString(nickname)}", ct);
            if (!response.IsSuccessStatusCode) return null;

            var html = await response.Content.ReadAsStringAsync(ct);
            return Parse(html);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch STALCRAFT profile for {Nickname}", nickname);
            return null;
        }
    }

    public static PlayerProfile? Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var kills = ParseStat(doc, "Kills");
        var deaths = ParseStat(doc, "Deaths");

        if (kills == 0 && deaths == 0) return null;

        var accuracy = ParseText(doc, "Accuracy") ?? "—";
        var playtime = ParsePlaytime(doc) ?? "—";
        var clanHistory = ParseClanHistory(doc);
        var kd = deaths > 0 ? Math.Round(kills / (double)deaths, 2) : (double)kills;

        return new PlayerProfile(kills, deaths, kd, accuracy, playtime, clanHistory);
    }

    private static int ParseStat(HtmlDocument doc, string label)
    {
        var raw = GetDdValue(doc, label);
        if (raw is null) return 0;
        var cleaned = Regex.Replace(
            HtmlEntity.DeEntitize(raw).Trim(),
            @"[\s ,]", "");
        return int.TryParse(cleaned, out var n) ? n : 0;
    }

    private static string? ParseText(HtmlDocument doc, string label)
    {
        var raw = GetDdValue(doc, label);
        return raw is null ? null : HtmlEntity.DeEntitize(raw).Trim();
    }

    private static string? GetDdValue(HtmlDocument doc, string label)
    {
        var dt = doc.DocumentNode.SelectSingleNode(
            $"//dt[normalize-space(.)='{label}:' or normalize-space(.)='{label}']");
        if (dt is null) return null;

        var dd = dt.SelectSingleNode("following-sibling::dd[1]")
                ?? dt.ParentNode?.SelectSingleNode(".//dd");
        return dd?.InnerText;
    }

    private static string? ParsePlaytime(HtmlDocument doc)
    {
        const string marker = "In-game for ";
        var node = doc.DocumentNode
            .SelectSingleNode($"//*[contains(normalize-space(.), '{marker}')]");
        if (node is null) return null;

        var text = HtmlEntity.DeEntitize(node.InnerText).Trim();
        var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        return text[(idx + marker.Length)..]
            .Split('\n')[0]
            .Trim();
    }

    private static IReadOnlyList<ClanEntry> ParseClanHistory(HtmlDocument doc)
    {
        var entries = new List<ClanEntry>();
        var seen = new HashSet<string>();

        var nodes = doc.DocumentNode.SelectNodes(
            "//*[contains(text(),'[') and contains(text(),']')]");
        if (nodes is null) return entries;

        foreach (var node in nodes)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText).Trim();
            var tagMatch = Regex.Match(text, @"\[([A-Z0-9]{1,8})\]");
            if (!tagMatch.Success || !seen.Add(tagMatch.Groups[1].Value)) continue;

            var tag = tagMatch.Groups[1].Value;
            var name = Regex.Replace(text, @"\[[A-Z0-9]+\]", "").Trim();
            if (string.IsNullOrEmpty(name)) continue;

            entries.Add(new ClanEntry(name, tag, ""));
        }
        return entries;
    }
}
