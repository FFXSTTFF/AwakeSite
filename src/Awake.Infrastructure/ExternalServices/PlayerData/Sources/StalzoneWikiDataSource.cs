using Awake.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Awake.Infrastructure.ExternalServices.PlayerData.Sources;

public sealed class StalzoneWikiDataSource(ILogger<StalzoneWikiDataSource> logger)
    : IPlayerDataSource, IAsyncDisposable
{
    private const string BaseUrl = "https://stalzone.wiki";
    private const string Server  = "RU";

    private IPlaywright? _playwright;
    private IBrowser?    _browser;
    private readonly SemaphoreSlim _init = new(1, 1);

    public async Task<PlayerProfile?> TryGetDataAsync(string nickname, CancellationToken ct = default)
    {
        logger.LogInformation("Fetching player profile via stalzone.wiki for {Nickname}", nickname);
        try
        {
            var browser = await GetBrowserAsync(ct);
            await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent    = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                Locale       = "ru-RU"
            });

            var page = await context.NewPageAsync();
            try
            {
                var url = $"{BaseUrl}/characters/{Server}/{Uri.EscapeDataString(nickname)}";
                var response = await page.GotoAsync(url, new PageGotoOptions
                {
                    Timeout   = 30_000,
                    WaitUntil = WaitUntilState.DOMContentLoaded
                });

                logger.LogInformation("stalzone.wiki response: {Status}", response?.Status);
                if (response is null || !response.Ok) return null;

                // Wait for React to render stats — look for a numeric value in the kills slot
                try
                {
                    await page.WaitForFunctionAsync(
                        "() => document.body.innerText.length > 500",
                        null,
                        new PageWaitForFunctionOptions { Timeout = 15_000 });
                }
                catch (TimeoutException)
                {
                    logger.LogWarning("Timeout waiting for stalzone.wiki content");
                }

                // Extract stats via JS evaluation — finds numbers associated with known EXBO stat labels
                var stats = await page.EvaluateAsync<Dictionary<string, string>>(@"() => {
                    const result = {};
                    const allText = document.querySelectorAll('*');

                    // stalzone.wiki renders stats as pairs of label+value
                    // Walk all elements and find ones containing only a large number (stat values)
                    document.querySelectorAll('span, p, div, td, dd, strong').forEach(el => {
                        const text = (el.innerText || '').trim();
                        const num = text.replace(/[\s,]/g, '');
                        if (/^\d{2,8}$/.test(num) && el.children.length === 0) {
                            // Check previous sibling or parent label
                            const label = (el.previousElementSibling?.innerText ||
                                           el.closest('[class*=""stat""], [class*=""kill""], [class*=""death""]')?.querySelector('span')?.innerText ||
                                           '').trim().toLowerCase();
                            if (label) result[label] = text;
                        }
                    });

                    // Also grab full page text for fallback parsing
                    result['__body__'] = document.body.innerText.substring(0, 3000);
                    return result;
                }");

                logger.LogInformation("DOM stats extracted: {@Stats}", stats?.Where(kv => kv.Key != "__body__"));
                if (stats?.TryGetValue("__body__", out var body) == true)
                    logger.LogDebug("Page body text:\n{Body}", body);

                return stats is null ? null : ParseFromBody(stats.GetValueOrDefault("__body__") ?? "");
            }
            finally
            {
                await page.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "stalzone.wiki fetch failed for {Nickname}", nickname);
            return null;
        }
    }

    private PlayerProfile? ParseFromBody(string bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText)) return null;

        // stalzone.wiki renders page text with labels on one line and values on the next (or same line)
        // Example body text structure to be confirmed after first run
        var lines = bodyText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        logger.LogDebug("Page has {LineCount} text lines", lines.Length);

        var kills  = FindIntAfterLabel(lines, "убийства", "kills");
        var deaths = FindIntAfterLabel(lines, "смерти", "deaths");

        if (kills == 0 && deaths == 0) return null;

        var kd       = deaths > 0 ? Math.Round(kills / (double)deaths, 2) : (double)kills;
        var accuracy = FindStringAfterLabel(lines, "точность", "accuracy") ?? "—";
        var playtime = FindStringAfterLabel(lines, "время", "playtime", "онлайн") ?? "—";

        return new PlayerProfile(kills, deaths, kd, accuracy, playtime, []);
    }

    private static int FindIntAfterLabel(string[] lines, params string[] labels)
    {
        for (var i = 0; i < lines.Length - 1; i++)
        {
            var lower = lines[i].ToLowerInvariant();
            if (labels.Any(l => lower.Contains(l)))
            {
                var next = lines[i + 1].Replace(" ", "").Replace(",", "").Replace(" ", "");
                if (int.TryParse(next, out var n)) return n;
            }
        }
        return 0;
    }

    private static string? FindStringAfterLabel(string[] lines, params string[] labels)
    {
        for (var i = 0; i < lines.Length - 1; i++)
        {
            var lower = lines[i].ToLowerInvariant();
            if (labels.Any(l => lower.Contains(l)))
            {
                var value = lines[i + 1].Trim();
                if (!string.IsNullOrEmpty(value) && value.Length < 50) return value;
            }
        }
        return null;
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
                Args           = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage"]
            });
            return _browser;
        }
        finally
        {
            _init.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) { await _browser.DisposeAsync(); _browser = null; }
        _playwright?.Dispose();
        _init.Dispose();
    }
}
