using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Awake.Domain.ValueObjects;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Awake.Infrastructure.ExternalServices.PlayerData.Sources;

// Fetches stalcrafthq.com via FlareSolverr (Cloudflare bypass).
// Stats come from server-rendered HTML; clan history from the JSON API using UUID + XSRF
// token extracted from the Blazor component-state comment embedded in the page.
public sealed class StalcraftHqDataSource(
    IHttpClientFactory factory,
    ILogger<StalcraftHqDataSource> logger) : IPlayerDataSource
{
    private const string StalcraftHqBase = "https://stalcrafthq.com";
    private const string Server          = "EU";
    private const string ClientVersion   = "4.11.20";

    public async Task<PlayerProfile?> TryGetDataAsync(string nickname, CancellationToken ct = default)
    {
        logger.LogInformation("Fetching {Nickname} via FlareSolverr → stalcrafthq.com", nickname);
        try
        {
            var page = await FetchPageAsync(nickname, ct);
            if (page is null) return null;

            var profile = ParseStats(page.Html);
            if (profile is null)
            {
                logger.LogWarning("stalcrafthq.com: stats parse returned null for {Nickname}", nickname);
                return null;
            }

            if (page.CharacterId is not null)
            {
                var history = await FetchClanHistoryAsync(
                    page.CharacterId, page.XsrfToken, page.CookieHeader, page.UserAgent, ct);
                profile = profile with { ClanHistory = history };
            }

            logger.LogInformation(
                "stalcrafthq.com: kills={Kills} deaths={Deaths} clans={Clans}",
                profile.Kills, profile.Deaths, profile.ClanHistory.Count);

            return profile;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FlareSolverr fetch failed for {Nickname}", nickname);
            return null;
        }
    }

    private sealed record PageData(
        string Html, string? CharacterId, string? XsrfToken,
        string CookieHeader, string UserAgent);

    private async Task<PageData?> FetchPageAsync(string nickname, CancellationToken ct)
    {
        var http      = factory.CreateClient("flaresolverr");
        var targetUrl = $"{StalcraftHqBase}/characters/{Server}/{Uri.EscapeDataString(nickname)}";

        var resp = await http.PostAsJsonAsync("v1", new
        {
            cmd        = "request.get",
            url        = targetUrl,
            maxTimeout = 60_000
        }, ct);

        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("FlareSolverr returned {Status}", resp.StatusCode);
            return null;
        }

        var json     = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        var solution = json.GetProperty("solution");
        var status   = solution.GetProperty("status").GetInt32();
        var html     = solution.GetProperty("response").GetString();

        logger.LogInformation("stalcrafthq.com via FlareSolverr: HTTP {Status}", status);
        if (status != 200 || string.IsNullOrEmpty(html)) return null;

        var cookieHeader = "";
        if (solution.TryGetProperty("cookies", out var cookiesEl))
        {
            cookieHeader = string.Join("; ", cookiesEl.EnumerateArray()
                .Select(c =>
                    $"{c.GetProperty("name").GetString()}={c.GetProperty("value").GetString()}"));
        }

        var userAgent = solution.TryGetProperty("userAgent", out var ua) ? ua.GetString() ?? "" : "";

        // FlareSolverr returns fully-hydrated HTML (Blazor WASM has run and removed the state comment).
        // Make a second plain HTTP GET with CF cookies to get the server's pre-rendered HTML,
        // which still contains <!--Blazor-WebAssembly-Component-State:...--> with the character UUID.
        var (characterId, xsrfToken) = await ExtractFromPreRenderedPageAsync(
            targetUrl, cookieHeader, userAgent, ct);

        logger.LogInformation("Blazor state: characterId={CharacterId} xsrf={HasXsrf}",
            characterId, xsrfToken is not null);

        return new PageData(html, characterId, xsrfToken, cookieHeader, userAgent);
    }

    // FlareSolverr returns fully-hydrated HTML where Blazor has already removed the state comment.
    // A plain HTTP GET with the CF clearance cookies returns the server's pre-rendered HTML,
    // which still has the state comment intact (no JavaScript runs server-side).
    private async Task<(string? CharacterId, string? XsrfToken)> ExtractFromPreRenderedPageAsync(
        string url, string cookieHeader, string userAgent, CancellationToken ct)
    {
        try
        {
            var http = factory.CreateClient("stalcrafthq-api");
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            req.Headers.TryAddWithoutValidation("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            if (!string.IsNullOrEmpty(cookieHeader))
                req.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Pre-rendered page fetch returned {Status}", resp.StatusCode);
                return (null, null);
            }

            var html = await resp.Content.ReadAsStringAsync(ct);
            return ExtractFromBlazorState(html);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch pre-rendered page for Blazor state");
            return (null, null);
        }
    }

    // The page embeds a <!--Blazor-WebAssembly-Component-State:base64--> comment.
    // Decoded JSON contains _characterID and __internal__AntiforgeryTokenProvider,
    // each stored as a base64-encoded JSON string (double-encoded by Blazor).
    private static (string? CharacterId, string? XsrfToken) ExtractFromBlazorState(string html)
    {
        const string marker = "<!--Blazor-WebAssembly-Component-State:";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return (null, null);

        start += marker.Length;
        var end = html.IndexOf("-->", start, StringComparison.Ordinal);
        if (end <= start) return (null, null);

        try
        {
            var stateJson = Encoding.UTF8.GetString(
                Convert.FromBase64String(html[start..end]));

            using var doc = JsonDocument.Parse(stateJson);
            var root = doc.RootElement;

            string? characterId = null;
            string? xsrfToken   = null;

            foreach (var prop in root.EnumerateObject())
            {
                if (characterId is null &&
                    prop.Name.Contains("_characterID", StringComparison.OrdinalIgnoreCase))
                {
                    characterId = DecodeBlazorString(prop.Value.GetString());
                }
                else if (xsrfToken is null &&
                         prop.Name == "__internal__AntiforgeryTokenProvider")
                {
                    xsrfToken = DecodeBlazorString(prop.Value.GetString());
                }

                if (characterId is not null && xsrfToken is not null) break;
            }

            return (characterId, xsrfToken);
        }
        catch
        {
            return (null, null);
        }
    }

    // Blazor stores component state values as base64(json-string), so the decoded bytes
    // are a JSON string literal — strip the surrounding quotes to get the raw value.
    private static string? DecodeBlazorString(string? encoded)
    {
        if (encoded is null) return null;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            return raw.Trim().Trim('"');
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<ClanEntry>> FetchClanHistoryAsync(
        string characterId, string? xsrfToken, string cookieHeader, string userAgent,
        CancellationToken ct)
    {
        try
        {
            var http = factory.CreateClient("stalcrafthq-api");
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"{StalcraftHqBase}/api/characters/{characterId}/clan/history");

            req.Headers.TryAddWithoutValidation("client-version", ClientVersion);
            req.Headers.TryAddWithoutValidation("User-Agent",
                string.IsNullOrEmpty(userAgent)
                    ? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
                    : userAgent);

            if (!string.IsNullOrEmpty(xsrfToken))
                req.Headers.TryAddWithoutValidation("xsrf-token", xsrfToken);
            if (!string.IsNullOrEmpty(cookieHeader))
                req.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Clan history API returned {Status} for characterId={CharacterId}",
                    resp.StatusCode, characterId);
                return [];
            }

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
            return ParseClanHistoryJson(json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Clan history fetch failed for characterId={CharacterId}", characterId);
            return [];
        }
    }

    private static IReadOnlyList<ClanEntry> ParseClanHistoryJson(JsonElement json)
    {
        var entries = new List<ClanEntry>();
        var seen    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in json.EnumerateArray())
        {
            if (!item.TryGetProperty("clan", out var clan) || clan.ValueKind == JsonValueKind.Null)
                continue;

            var tag  = clan.GetProperty("tag").GetString()  ?? "";
            var name = clan.GetProperty("name").GetString() ?? "";
            if (!string.IsNullOrEmpty(tag) && seen.Add(tag))
                entries.Add(new ClanEntry(name, tag, ""));
        }

        return entries;
    }

    // stalcrafthq.com server-renders stats as: <p><b>Kills:</b></p><p>237 597</p>
    internal static PlayerProfile? ParseStats(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var kills  = ParseBoldLabelInt(doc, "Kills");
        var deaths = ParseBoldLabelInt(doc, "Deaths");
        if (kills == 0 && deaths == 0) return null;

        var accuracy = ParseBoldLabelText(doc, "Accuracy") ?? "—";
        var kd       = deaths > 0 ? Math.Round(kills / (double)deaths, 2) : (double)kills;
        var playtime = ParsePlaytime(doc) ?? "—";

        return new PlayerProfile(kills, deaths, kd, accuracy, playtime, []);
    }

    private static int ParseBoldLabelInt(HtmlDocument doc, string label)
    {
        var text = ParseBoldLabelText(doc, label);
        if (text is null) return 0;
        var cleaned = Regex.Replace(text, @"[\s  ,]", "");
        return int.TryParse(cleaned, out var n) ? n : 0;
    }

    private static string? ParseBoldLabelText(HtmlDocument doc, string label)
    {
        var node = doc.DocumentNode.SelectSingleNode(
            $"//p[b[normalize-space()='{label}:']]/following-sibling::p[1]");
        return node is null ? null : HtmlEntity.DeEntitize(node.InnerText).Trim();
    }

    // stalcrafthq: <p><span>In-game for </span></p> followed by <p><span class="inline-timespan ...">454 days …</span></p>
    private static string? ParsePlaytime(HtmlDocument doc)
    {
        var node = doc.DocumentNode.SelectSingleNode(
            "//p[span[contains(., 'In-game for')]]/following-sibling::p[1]//span");
        return node is null ? null : HtmlEntity.DeEntitize(node.InnerText).Trim();
    }
}
