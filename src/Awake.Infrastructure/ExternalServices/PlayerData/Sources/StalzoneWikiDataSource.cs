using System.Globalization;
using Awake.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Awake.Infrastructure.ExternalServices.PlayerData.Sources;

public sealed class StalzoneWikiDataSource(ILogger<StalzoneWikiDataSource> logger)
    : IPlayerDataSource, IAsyncDisposable
{
    private const string BaseUrl = "https://stalzone.wiki";
    private const string Server  = "EU";

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
                    Timeout   = 45_000,
                    WaitUntil = WaitUntilState.NetworkIdle
                });

                logger.LogInformation("stalzone.wiki response: {Status}", response?.Status);
                if (response is null || !response.Ok) return null;

                await page.WaitForTimeoutAsync(500);

                var bodyText = await page.EvaluateAsync<string>("() => document.body.innerText");
                return bodyText is null ? null : ParseFromBody(bodyText);
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

    // stalzone.wiki summary block layout (confirmed from body text):
    //   УБИЙСТВ\n50 812\nСМЕРТЕЙ\n39 742\nK/D\n1.28\nВРЕМЕНИ В ИГРЕ\n1902 ч.\nГРУППИРОВКА\nЗавет
    private PlayerProfile? ParseFromBody(string bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText)) return null;

        if (bodyText.Contains("Игрок не найден", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("stalzone.wiki: player not found");
            return null;
        }

        var lines = bodyText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var kills  = FindIntAfterLabel(lines, "убийств");
        var deaths = FindIntAfterLabel(lines, "смертей");

        if (kills == 0 && deaths == 0) return null;

        var kd = deaths > 0 ? Math.Round(kills / (double)deaths, 2) : (double)kills;

        var kdStr = FindValueAfterLabel(lines, "k/d");
        if (kdStr != null && double.TryParse(kdStr.Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var kdParsed))
            kd = kdParsed;

        var playtime = FindValueAfterLabel(lines, "времени в игре") ?? "—";
        // stalzone.wiki shows faction (Завет/Наёмник/etc.), not player clan — clan history comes from FlareSolverr
        var clanHistory = Array.Empty<ClanEntry>();

        logger.LogInformation("Parsed stalzone.wiki profile: kills={Kills} deaths={Deaths} kd={Kd}",
            kills, deaths, kd);

        return new PlayerProfile(kills, deaths, kd, "—", playtime, (IReadOnlyList<ClanEntry>)clanHistory);
    }

    private static int FindIntAfterLabel(string[] lines, string label)
    {
        for (var i = 0; i < lines.Length - 1; i++)
        {
            if (!lines[i].Equals(label, StringComparison.OrdinalIgnoreCase)) continue;

            // Strip regular space (U+0020), non-breaking space (U+00A0), narrow no-break space (U+202F)
            var next = lines[i + 1]
                .Replace(" ", "").Replace(" ", "").Replace(" ", "").Replace(",", "");
            if (int.TryParse(next, out var n)) return n;
        }
        return 0;
    }

    private static string? FindValueAfterLabel(string[] lines, string label)
    {
        for (var i = 0; i < lines.Length - 1; i++)
        {
            if (!lines[i].Equals(label, StringComparison.OrdinalIgnoreCase)) continue;
            var value = lines[i + 1].Trim();
            return string.IsNullOrEmpty(value) ? null : value;
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
