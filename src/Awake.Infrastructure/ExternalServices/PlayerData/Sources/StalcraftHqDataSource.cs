using System.Text.RegularExpressions;
using Awake.Domain.ValueObjects;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Awake.Infrastructure.ExternalServices.PlayerData.Sources;

public sealed class StalcraftHqDataSource(ILogger<StalcraftHqDataSource> logger)
    : IPlayerDataSource, IAsyncDisposable
{
    private const string BaseUrl = "https://stalcrafthq.com";
    private const string Server  = "EU";

    private IPlaywright? _playwright;
    private IBrowser?    _browser;
    private readonly SemaphoreSlim _init = new(1, 1);

    public async Task<PlayerProfile?> TryGetDataAsync(string nickname, CancellationToken ct = default)
    {
        logger.LogInformation("Fetching player profile via Playwright for {Nickname}", nickname);
        try
        {
            var browser = await GetBrowserAsync(ct);
            await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                            "AppleWebKit/537.36 (KHTML, like Gecko) " +
                            "Chrome/125.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                Locale    = "ru-RU"
            });

            // Patch navigator to look like a real browser, not a bot
            await context.AddInitScriptAsync("""
                Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
                Object.defineProperty(navigator, 'plugins',   { get: () => [1, 2, 3, 4, 5] });
                Object.defineProperty(navigator, 'languages', { get: () => ['ru-RU', 'ru', 'en-US', 'en'] });
                """);

            var page = await context.NewPageAsync();
            try
            {
                var url = $"{BaseUrl}/characters/{Server}/{Uri.EscapeDataString(nickname)}";
                logger.LogInformation("Navigating to {Url}", url);
                var response = await page.GotoAsync(url, new PageGotoOptions
                {
                    Timeout   = 30_000,
                    WaitUntil = WaitUntilState.DOMContentLoaded
                });

                logger.LogInformation("Page response status: {Status}", response?.Status);
                if (response is null || !response.Ok) return null;

                try { await page.WaitForSelectorAsync("dt", new PageWaitForSelectorOptions { Timeout = 10_000 }); }
                catch (TimeoutException) { logger.LogWarning("Timeout waiting for <dt> — page has no stats section"); }

                var html = await page.ContentAsync();
                logger.LogInformation("Got HTML ({Length} chars), parsing...", html.Length);
                var profile = Parse(html);
                logger.LogInformation("Parse result: {Result}", profile is null ? "null" : $"kills={profile.Kills}");
                return profile;
            }
            finally
            {
                await page.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Playwright failed to fetch STALCRAFT profile for {Nickname}", nickname);
            return null;
        }
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken ct)
    {
        if (_browser is not null) return _browser;

        await _init.WaitAsync(ct);
        try
        {
            if (_browser is not null) return _browser;

            _playwright = await Playwright.CreateAsync();

            var execPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_CHROMIUM_PATH");

            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless       = true,
                ExecutablePath = string.IsNullOrEmpty(execPath) ? null : execPath,
                Args           =
                [
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-dev-shm-usage",
                    // Prevent bot detection via navigator.webdriver flag
                    "--disable-blink-features=AutomationControlled"
                ]
            });

            return _browser;
        }
        finally
        {
            _init.Release();
        }
    }

    // ── HTML parsing (HtmlAgilityPack) ───────────────────────────────────────

    public static PlayerProfile? Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var kills  = ParseStat(doc, "Kills");
        var deaths = ParseStat(doc, "Deaths");

        if (kills == 0 && deaths == 0) return null;

        var accuracy    = ParseText(doc, "Accuracy") ?? "—";
        var playtime    = ParsePlaytime(doc) ?? "—";
        var clanHistory = ParseClanHistory(doc);
        var kd          = deaths > 0 ? Math.Round(kills / (double)deaths, 2) : (double)kills;

        return new PlayerProfile(kills, deaths, kd, accuracy, playtime, clanHistory);
    }

    private static int ParseStat(HtmlDocument doc, string label)
    {
        var raw = GetDdValue(doc, label);
        if (raw is null) return 0;
        var cleaned = Regex.Replace(HtmlEntity.DeEntitize(raw).Trim(), @"[\s ,]", "");
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
        var idx  = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        return text[(idx + marker.Length)..].Split('\n')[0].Trim();
    }

    private static IReadOnlyList<ClanEntry> ParseClanHistory(HtmlDocument doc)
    {
        var entries = new List<ClanEntry>();
        var seen    = new HashSet<string>();

        var nodes = doc.DocumentNode.SelectNodes(
            "//*[contains(text(),'[') and contains(text(),']')]");
        if (nodes is null) return entries;

        foreach (var node in nodes)
        {
            var text     = HtmlEntity.DeEntitize(node.InnerText).Trim();
            var tagMatch = Regex.Match(text, @"\[([A-Z0-9]{1,8})\]");
            if (!tagMatch.Success || !seen.Add(tagMatch.Groups[1].Value)) continue;

            var tag  = tagMatch.Groups[1].Value;
            var name = Regex.Replace(text, @"\[[A-Z0-9]+\]", "").Trim();
            if (string.IsNullOrEmpty(name)) continue;

            entries.Add(new ClanEntry(name, tag, ""));
        }
        return entries;
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
            _browser = null;
        }
        _playwright?.Dispose();
        _init.Dispose();
    }
}
