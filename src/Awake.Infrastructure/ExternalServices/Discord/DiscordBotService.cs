using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Awake.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Awake.Infrastructure.ExternalServices.Discord;

public class DiscordBotService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<DiscordBotService> logger) : IDiscordBotService
{
    private const string ApiBase = "https://discord.com/api/v10";
    private const int BrandColor = 4054148; // #3ddc84

    private string? _botUserId;

    private async Task<string?> GetBotUserIdAsync(CancellationToken ct = default)
    {
        if (_botUserId is not null) return _botUserId;
        try
        {
            var resp = await httpClient.GetAsync($"{ApiBase}/users/@me", ct);
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(ct);
            _botUserId = doc?.RootElement.GetProperty("id").GetString();
            return _botUserId;
        }
        catch { return null; }
    }

    private void SetAuth()
    {
        httpClient.DefaultRequestHeaders.Remove("Authorization");
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bot {configuration["Discord:BotToken"]}");
    }

    private bool EnsureConfigured()
    {
        if (!string.IsNullOrWhiteSpace(configuration["Discord:BotToken"])) return true;
        logger.LogWarning("Discord:BotToken is not configured. Skipping Discord action.");
        return false;
    }

    // ── DM ──────────────────────────────────────────────────────────────────

    public async Task SendDmAsync(string discordUserId, string message, CancellationToken ct = default)
    {
        if (!EnsureConfigured()) return;
        SetAuth();
        try
        {
            var dmChannelId = await OpenDmChannelAsync(discordUserId, ct);
            if (dmChannelId is null) return;
            await httpClient.PostAsJsonAsync($"{ApiBase}/channels/{dmChannelId}/messages",
                new { content = message }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send DM to {UserId}", discordUserId);
        }
    }

    // ── Post button message in public channel ────────────────────────────────

    public async Task PostApplicationButtonAsync(string channelId, CancellationToken ct = default)
    {
        if (!EnsureConfigured()) return;
        SetAuth();
        try
        {
            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = "📋 Awake [LOVE] — Clan Application",
                        description = "Want to join our clan?\n\n" +
                                      "**Requirements:**\n" +
                                      "• Active STALCRAFT player\n" +
                                      "• Teamplay mindset\n" +
                                      "• Ready to follow clan rules\n\n" +
                                      "Click the button below to submit your application.",
                        color = BrandColor
                    }
                },
                components = new[]
                {
                    new
                    {
                        type = 1,
                        components = new[]
                        {
                            new
                            {
                                type = 2,
                                style = 3, // SUCCESS (green)
                                label = "Submit Application",
                                custom_id = "open_ticket",
                                emoji = new { name = "📝" }
                            }
                        }
                    }
                }
            };

            var resp = await httpClient.PostAsJsonAsync($"{ApiBase}/channels/{channelId}/messages", payload, ct);
            if (!resp.IsSuccessStatusCode)
                logger.LogWarning("Failed to post button message to {ChannelId}: {Status}", channelId, resp.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to post application button to {ChannelId}", channelId);
        }
    }

    // ── Get channel parent (category) ID ────────────────────────────────────

    public async Task<string?> GetChannelParentIdAsync(string channelId, CancellationToken ct = default)
    {
        if (!EnsureConfigured()) return null;
        SetAuth();
        try
        {
            var resp = await httpClient.GetAsync($"{ApiBase}/channels/{channelId}", ct);
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(ct);
            return doc?.RootElement.TryGetProperty("parent_id", out var pid) == true ? pid.GetString() : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch channel {ChannelId}", channelId);
            return null;
        }
    }

    // ── Create private ticket channel in category ────────────────────────────

    public async Task<string?> CreateTicketChannelAsync(
        string guildId,
        string categoryId,
        string userId,
        string username,
        string? adminRoleId,
        string gameNickname,
        CancellationToken ct = default)
    {
        if (!EnsureConfigured()) return null;
        SetAuth();
        try
        {
            var safeName = SanitizeChannelName($"ticket-{username}");

            const long allowBits = 1024L + 2048L + 65536L; // VIEW + SEND + READ_HISTORY
            const long denyBits  = allowBits;

            var botId = await GetBotUserIdAsync(ct);

            var overwrites = new List<object>
            {
                new { id = guildId, type = 0, allow = "0",                 deny = denyBits.ToString() },
                new { id = userId,  type = 1, allow = allowBits.ToString(), deny = "0" },
            };
            if (!string.IsNullOrEmpty(botId))
                overwrites.Add(new { id = botId, type = 1, allow = allowBits.ToString(), deny = "0" });
            if (!string.IsNullOrEmpty(adminRoleId))
                overwrites.Add(new { id = adminRoleId, type = 0, allow = allowBits.ToString(), deny = "0" });

            var payload = new
            {
                name = safeName,
                type = 0,
                parent_id = categoryId,
                topic = $"Application ticket for {gameNickname}",
                permission_overwrites = overwrites
            };

            var resp = await httpClient.PostAsJsonAsync($"{ApiBase}/guilds/{guildId}/channels", payload, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to create ticket channel: {Status} {Body}",
                    resp.StatusCode, await resp.Content.ReadAsStringAsync(ct));
                return null;
            }
            using var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(ct);
            return doc?.RootElement.GetProperty("id").GetString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create ticket channel for user {UserId}", userId);
            return null;
        }
    }

    // ── Post ticket embed in the private channel ──────────────────────────────

    public async Task PostTicketEmbedAsync(
        string channelId,
        Guid ticketId,
        string gameNickname,
        string description,
        string discordUsername,
        CancellationToken ct = default)
    {
        if (!EnsureConfigured()) return;
        SetAuth();
        try
        {
            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = "📋 Application Details",
                        color = BrandColor,
                        description = $"**About the applicant:**\n{description}",
                        fields = new[]
                        {
                            new { name = "Applicant", value = discordUsername, inline = true },
                            new { name = "Game Nickname", value = gameNickname, inline = true },
                            new { name = "Status", value = "⏳ Pending review", inline = false }
                        },
                        footer = new { text = "Officers only: use the buttons below to make a decision." }
                    }
                },
                components = new[]
                {
                    new
                    {
                        type = 1,
                        components = new object[]
                        {
                            new
                            {
                                type = 2,
                                style = 3, // SUCCESS (green)
                                label = "Approve",
                                custom_id = $"approve_ticket:{ticketId}",
                                emoji = new { name = "✅" }
                            },
                            new
                            {
                                type = 2,
                                style = 4, // DANGER (red)
                                label = "Reject",
                                custom_id = $"reject_ticket:{ticketId}",
                                emoji = new { name = "❌" }
                            }
                        }
                    }
                }
            };
            await httpClient.PostAsJsonAsync($"{ApiBase}/channels/{channelId}/messages", payload, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to post ticket embed to {ChannelId}", channelId);
        }
    }

    // ── Notify admin feed channel ─────────────────────────────────────────────

    public async Task PostAdminEmbedAsync(
        string adminChannelId,
        string ticketChannelId,
        string gameNickname,
        string discordUsername,
        CancellationToken ct = default)
    {
        if (!EnsureConfigured()) return;
        SetAuth();
        try
        {
            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = "📥 New Ticket",
                        color = BrandColor,
                        fields = new[]
                        {
                            new { name = "Applicant", value = discordUsername, inline = true },
                            new { name = "Nickname", value = gameNickname, inline = true },
                            new { name = "Channel", value = $"<#{ticketChannelId}>", inline = false }
                        }
                    }
                }
            };
            await httpClient.PostAsJsonAsync($"{ApiBase}/channels/{adminChannelId}/messages", payload, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to post admin embed to {ChannelId}", adminChannelId);
        }
    }

    // ── Post status update in ticket channel ──────────────────────────────────

    public async Task PostStatusUpdateAsync(string channelId, string statusText, CancellationToken ct = default)
    {
        if (!EnsureConfigured()) return;
        SetAuth();
        try
        {
            await httpClient.PostAsJsonAsync($"{ApiBase}/channels/{channelId}/messages",
                new { content = statusText }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to post status update to {ChannelId}", channelId);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string?> OpenDmChannelAsync(string userId, CancellationToken ct)
    {
        var resp = await httpClient.PostAsJsonAsync($"{ApiBase}/users/@me/channels",
            new { recipient_id = userId }, ct);
        if (!resp.IsSuccessStatusCode) return null;
        using var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(ct);
        return doc?.RootElement.GetProperty("id").GetString();
    }

    private static string SanitizeChannelName(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_')
                sb.Append(c);
            else if (c == ' ')
                sb.Append('-');
        }
        var result = sb.ToString().Trim('-');
        return result.Length > 100 ? result[..100] : result;
    }
}
