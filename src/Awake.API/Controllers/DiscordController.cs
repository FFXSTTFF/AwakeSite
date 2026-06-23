using System.Text;
using System.Text.Json;
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Tickets.Commands.AddTicketComment;
using Awake.Application.Features.Tickets.Commands.CreateDiscordTicket;
using Awake.Application.Features.Tickets.Commands.UpdateTicketStatus;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSec.Cryptography;

namespace Awake.API.Controllers;

[ApiController]
[Route("api/discord")]
public class DiscordController(
    IMediator mediator,
    IConfiguration configuration,
    IDiscordBotService discordBotService,
    IDiscordGuildSettingsRepository guildSettingsRepository,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DiscordController> logger) : ControllerBase
{
    // ── Main interaction endpoint ─────────────────────────────────────────────

    [HttpPost("interactions")]
    public async Task<IActionResult> Interactions()
    {
        Request.EnableBuffering();
        var bodyBytes = await ReadBodyAsync();
        Request.Body.Position = 0;

        var timestamp = Request.Headers["X-Signature-Timestamp"].ToString();
        var signature = Request.Headers["X-Signature-Ed25519"].ToString();
        if (!VerifySignature(configuration["Discord:PublicKey"] ?? string.Empty, timestamp, signature, bodyBytes))
            return Unauthorized();

        using var doc = JsonDocument.Parse(bodyBytes);
        var root = doc.RootElement;
        var type = root.GetProperty("type").GetInt32();

        return type switch
        {
            1 => Ok(new { type = 1 }),
            2 => await HandleApplicationCommand(root),
            3 => await HandleMessageComponent(root),
            5 => await HandleModalSubmit(root),
            _ => Ok(new { type = 1 })
        };
    }

    // ── Register slash commands (one-time admin call) ──────────────────────────

    [HttpPost("register-commands")]
    public async Task<IActionResult> RegisterCommands()
    {
        var botToken = configuration["Discord:BotToken"];
        var appId    = configuration["Discord:ApplicationId"];
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(appId))
            return BadRequest("Discord:BotToken and Discord:ApplicationId must be configured.");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bot {botToken}");

        var commands = new object[]
        {
            new
            {
                name = "ticket",
                type = 1,
                description = "Submit an application to join clan Awake [LOVE] (direct modal)"
            },
            new
            {
                name = "szticket",
                type = 1,
                description = "Post the application button message in this channel"
            },
            new
            {
                name = "szticketadm",
                type = 1,
                description = "Set this channel as admin ticket feed",
                options = new[]
                {
                    new
                    {
                        name = "role",
                        description = "Officer/Admin role that gets access to ticket channels",
                        type = 8,      // ROLE
                        required = false
                    }
                }
            }
        };

        var results = new List<string>();
        foreach (var cmd in commands)
        {
            var resp = await http.PostAsJsonAsync(
                $"https://discord.com/api/v10/applications/{appId}/commands", cmd);
            results.Add($"{(resp.IsSuccessStatusCode ? "✅" : "❌")} {resp.StatusCode}");
        }

        return Ok(string.Join("\n", results));
    }

    // ── APPLICATION_COMMAND handlers ──────────────────────────────────────────

    private async Task<IActionResult> HandleApplicationCommand(JsonElement root)
    {
        var commandName = root.GetProperty("data").GetProperty("name").GetString();
        var guildId     = root.TryGetProperty("guild_id", out var gid) ? gid.GetString() : null;
        var channelId   = root.TryGetProperty("channel_id", out var cid) ? cid.GetString() : null;

        return commandName switch
        {
            "ticket"      => Ok(BuildTicketModal()),
            "szticket"    => await HandleSetupTicketChannel(guildId, channelId),
            "szticketadm" => await HandleSetupAdminChannel(root, guildId, channelId),
            _             => Ok(new { type = 1 })
        };
    }

    // /szticket — posts embed + button as interaction response (not ephemeral → everyone sees it)
    private async Task<IActionResult> HandleSetupTicketChannel(string? guildId, string? channelId)
    {
        if (string.IsNullOrEmpty(guildId) || string.IsNullOrEmpty(channelId))
            return Ok(Ephemeral("❌ This command must be used in a server channel."));

        var categoryId = await discordBotService.GetChannelParentIdAsync(channelId);

        await guildSettingsRepository.UpsertAsync(new DiscordGuildSettings
        {
            GuildId = guildId,
            TicketCategoryId = categoryId
        });

        return Ok(new
        {
            type = 4,
            data = new
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
                        color = 4054148
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
                                style = 3,
                                label = "Submit Application",
                                custom_id = "open_ticket",
                                emoji = new { name = "📝" }
                            }
                        }
                    }
                }
            }
        });
    }

    // /szticketadm — saves admin channel ID and optional admin role
    private async Task<IActionResult> HandleSetupAdminChannel(JsonElement root, string? guildId, string? channelId)
    {
        if (string.IsNullOrEmpty(guildId) || string.IsNullOrEmpty(channelId))
            return Ok(Ephemeral("❌ This command must be used in a server channel."));

        string? roleId = null;
        if (root.GetProperty("data").TryGetProperty("options", out var options))
            foreach (var opt in options.EnumerateArray())
                if (opt.GetProperty("name").GetString() == "role")
                    roleId = opt.GetProperty("value").GetString();

        await guildSettingsRepository.UpsertAsync(new DiscordGuildSettings
        {
            GuildId = guildId,
            AdminChannelId = channelId,
            AdminRoleId = roleId
        });

        var rolePart = roleId is not null ? $" Ticket channels will be visible to <@&{roleId}>." : string.Empty;
        return Ok(Ephemeral($"✅ Admin feed configured for this channel.{rolePart}"));
    }

    // ── MESSAGE_COMPONENT (button click) ─────────────────────────────────────

    private async Task<IActionResult> HandleMessageComponent(JsonElement root)
    {
        var customId = root.GetProperty("data").GetProperty("custom_id").GetString() ?? string.Empty;

        if (customId == "open_ticket")
            return Ok(BuildTicketModal());

        if (customId.StartsWith("approve_ticket:") || customId.StartsWith("reject_ticket:"))
            return HandleTicketDecision(root, customId);

        if (customId == "close_channel")
            return await HandleCloseChannel(root);

        return Ok(new { type = 1 });
    }

    private IActionResult HandleTicketDecision(JsonElement root, string customId)
    {
        var guildId = root.TryGetProperty("guild_id", out var gid) ? gid.GetString() : null;
        var interactionToken = root.TryGetProperty("token", out var tok) ? tok.GetString() : null;
        var applicationId = configuration["Discord:ApplicationId"];

        // Officer check (synchronous data extraction)
        HashSet<string?> memberRoles = [];
        if (root.TryGetProperty("member", out var mem))
        {
            if (mem.TryGetProperty("roles", out var roles))
                memberRoles = roles.EnumerateArray().Select(r => r.GetString()).ToHashSet();
        }

        var discordUsername = mem.ValueKind != JsonValueKind.Undefined &&
                              mem.TryGetProperty("user", out var u) &&
                              u.TryGetProperty("username", out var un)
            ? un.GetString()
            : null;

        var parts = customId.Split(':', 2);
        if (parts.Length < 2 || !Guid.TryParse(parts[1], out var ticketId))
            return Ok(Ephemeral("❌ Invalid ticket reference."));

        var isApprove = customId.StartsWith("approve_ticket:");
        var newStatus = isApprove ? TicketStatus.Approved : TicketStatus.Rejected;

        // Respond immediately to Discord (must be within 3 seconds)
        // All actual work runs in background, result sent as follow-up
        _ = Task.Run(async () =>
        {
            try
            {
                // Officer check using stored settings
                if (!string.IsNullOrEmpty(guildId))
                {
                    using var scope = httpContextAccessor.HttpContext!.RequestServices.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IDiscordGuildSettingsRepository>();
                    var bot = scope.ServiceProvider.GetRequiredService<IDiscordBotService>();
                    var med = scope.ServiceProvider.GetRequiredService<IMediator>();

                    var gs = await repo.GetByGuildIdAsync(guildId);
                    if (gs?.AdminRoleId is not null && !memberRoles.Contains(gs.AdminRoleId))
                    {
                        if (!string.IsNullOrEmpty(interactionToken) && !string.IsNullOrEmpty(applicationId))
                            await bot.FollowUpAsync(applicationId, interactionToken, "❌ Only officers can make this decision.");
                        return;
                    }

                    var result = await med.Send(new UpdateTicketStatusCommand(ticketId, newStatus, discordUsername));
                    if (!string.IsNullOrEmpty(interactionToken) && !string.IsNullOrEmpty(applicationId))
                    {
                        var msg = result.IsSuccess
                            ? (isApprove ? "✅ Application **approved**." : "❌ Application **rejected**.")
                            : $"❌ {result.Error}";
                        await bot.FollowUpAsync(applicationId, interactionToken, msg);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background ticket decision failed");
            }
        });

        // Deferred ephemeral response — tells Discord "we're working on it"
        return Ok(new { type = 5, data = new { flags = 64 } });
    }

    private IActionResult HandleAddCommentButton(string customId)
    {
        var ticketId = customId["add_comment:".Length..];
        return Ok(new
        {
            type = 9,
            data = new
            {
                title = "Add Comment",
                custom_id = $"comment_modal:{ticketId}",
                components = new[]
                {
                    new
                    {
                        type = 1,
                        components = new[]
                        {
                            new
                            {
                                type = 4,
                                custom_id = "comment_text",
                                label = "Your comment",
                                style = 2,
                                required = true,
                                max_length = 1000,
                                placeholder = "Write your message here..."
                            }
                        }
                    }
                }
            }
        });
    }

    // ── MODAL_SUBMIT ──────────────────────────────────────────────────────────

    private async Task<IActionResult> HandleModalSubmit(JsonElement root)
    {
        var customId = root.GetProperty("data").GetProperty("custom_id").GetString() ?? string.Empty;

        if (customId.StartsWith("comment_modal:"))
            return await HandleCommentModalSubmit(root, customId);

        if (customId != "ticket_modal")
            return Ok(new { type = 1 });

        var (userId, username) = ExtractUser(root);
        var guildId = root.TryGetProperty("guild_id", out var gid) ? gid.GetString() : null;

        var values = ExtractModalValues(root.GetProperty("data").GetProperty("components"));
        values.TryGetValue("game_nickname", out var nickname);
        values.TryGetValue("description", out var description);

        if (string.IsNullOrWhiteSpace(nickname) || string.IsNullOrWhiteSpace(description))
            return Ok(Ephemeral("❌ Please fill in all fields."));

        // Create private ticket channel when guild settings exist
        string? ticketChannelId = null;
        DiscordGuildSettings? gs = null;

        if (!string.IsNullOrEmpty(guildId))
        {
            gs = await guildSettingsRepository.GetByGuildIdAsync(guildId);

            if (gs?.TicketCategoryId is not null)
            {
                ticketChannelId = await discordBotService.CreateTicketChannelAsync(
                    guildId, gs.TicketCategoryId, userId, username,
                    gs.AdminRoleId, nickname!);
            }
        }

        var result = await mediator.Send(new CreateDiscordTicketCommand(
            userId, username, nickname!, TicketType.Recruitment, description!, ticketChannelId));

        if (!result.IsSuccess)
            return Ok(Ephemeral("❌ Failed to submit your ticket. Please try again."));

        // Post embeds in background (avoid blocking the 3s interaction deadline)
        if (ticketChannelId is not null)
        {
            var capturedGs = gs;
            var ticketId = result.Value;
            _ = Task.Run(async () =>
            {
                await discordBotService.PostTicketEmbedAsync(
                    ticketChannelId, ticketId, nickname!, description!, username);

                if (capturedGs?.AdminChannelId is not null)
                    await discordBotService.PostAdminEmbedAsync(
                        capturedGs.AdminChannelId, ticketChannelId, nickname!, username);
            });
        }

        var reply = ticketChannelId is not null
            ? $"✅ Application submitted! Head to <#{ticketChannelId}> to see your ticket."
            : "✅ Application submitted! Officers will review it shortly.";

        return Ok(Ephemeral(reply));
    }

    private async Task<IActionResult> HandleCloseChannel(JsonElement root)
    {
        var guildId   = root.TryGetProperty("guild_id",   out var gid) ? gid.GetString() : null;
        var channelId = root.TryGetProperty("channel_id", out var cid) ? cid.GetString() : null;

        // Officer check
        if (!string.IsNullOrEmpty(guildId))
        {
            var gs = await guildSettingsRepository.GetByGuildIdAsync(guildId);
            if (gs?.AdminRoleId is not null)
            {
                var memberRoles = root.TryGetProperty("member", out var mem) &&
                                  mem.TryGetProperty("roles", out var roles)
                    ? roles.EnumerateArray().Select(r => r.GetString()).ToHashSet()
                    : [];

                if (!memberRoles.Contains(gs.AdminRoleId))
                    return Ok(Ephemeral("❌ Only officers can close the channel."));
            }
        }

        if (string.IsNullOrEmpty(channelId))
            return Ok(Ephemeral("❌ Could not determine channel."));

        // Mark ticket as Closed so website blocks further comments
        var scope = httpContextAccessor.HttpContext!.RequestServices.CreateScope();
        var ticketRepo = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
        var ticket = await ticketRepo.GetByDiscordChannelIdAsync(channelId);
        if (ticket is not null)
        {
            ticket.Status = TicketStatus.Closed;
            ticket.DiscordChannelId = null;
            await ticketRepo.UpdateAsync(ticket);
        }

        // Delete Discord channel after short delay so response is delivered first
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);
            await discordBotService.DeleteChannelAsync(channelId);
            scope.Dispose();
        });

        return Ok(Ephemeral("🗑️ Channel will be deleted shortly."));
    }

    private async Task<IActionResult> HandleCommentModalSubmit(JsonElement root, string customId)
    {
        var ticketIdStr = customId["comment_modal:".Length..];
        if (!Guid.TryParse(ticketIdStr, out var ticketId))
            return Ok(Ephemeral("❌ Invalid ticket reference."));

        var (_, username) = ExtractUser(root);
        var values = ExtractModalValues(root.GetProperty("data").GetProperty("components"));
        values.TryGetValue("comment_text", out var content);

        if (string.IsNullOrWhiteSpace(content))
            return Ok(Ephemeral("❌ Comment cannot be empty."));

        var result = await mediator.Send(new AddDiscordCommentCommand(ticketId, username, content!));

        return result.IsSuccess
            ? Ok(Ephemeral("✅ Your comment has been added."))
            : Ok(Ephemeral($"❌ {result.Error}"));
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static object BuildTicketModal() => new
    {
        type = 9,
        data = new
        {
            title = "Application to Awake [LOVE]",
            custom_id = "ticket_modal",
            components = new object[]
            {
                new
                {
                    type = 1,
                    components = new object[]
                    {
                        new
                        {
                            type = 4,
                            custom_id = "game_nickname",
                            label = "Game nickname in STALCRAFT",
                            style = 1,
                            required = true,
                            max_length = 100,
                            placeholder = "Your in-game nickname"
                        }
                    }
                },
                new
                {
                    type = 1,
                    components = new object[]
                    {
                        new
                        {
                            type = 4,
                            custom_id = "description",
                            label = "Tell us about yourself",
                            style = 2,
                            required = true,
                            max_length = 2000,
                            placeholder = "Experience, achievements, why you want to join..."
                        }
                    }
                }
            }
        }
    };

    private static object Ephemeral(string content) => new
    {
        type = 4,
        data = new { content, flags = 64 }
    };

    private static (string userId, string username) ExtractUser(JsonElement root)
    {
        if (root.TryGetProperty("member", out var member) &&
            member.TryGetProperty("user", out var mu))
        {
            var id   = mu.GetProperty("id").GetString()!;
            var name = (mu.TryGetProperty("global_name", out var gn) ? gn.GetString() : null)
                       ?? mu.GetProperty("username").GetString()!;
            return (id, name);
        }
        if (root.TryGetProperty("user", out var user))
        {
            var id   = user.GetProperty("id").GetString()!;
            var name = (user.TryGetProperty("global_name", out var gn) ? gn.GetString() : null)
                       ?? user.GetProperty("username").GetString()!;
            return (id, name);
        }
        return ("unknown", "unknown");
    }

    private static Dictionary<string, string> ExtractModalValues(JsonElement components)
    {
        var result = new Dictionary<string, string>();
        foreach (var row in components.EnumerateArray())
            foreach (var comp in row.GetProperty("components").EnumerateArray())
                result[comp.GetProperty("custom_id").GetString()!] =
                    comp.GetProperty("value").GetString() ?? string.Empty;
        return result;
    }

    private async Task<byte[]> ReadBodyAsync()
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        return ms.ToArray();
    }

    private static bool VerifySignature(string publicKeyHex, string timestamp, string signatureHex, byte[] body)
    {
        if (string.IsNullOrEmpty(publicKeyHex) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signatureHex))
            return false;
        try
        {
            var algorithm      = SignatureAlgorithm.Ed25519;
            var publicKeyBytes = Convert.FromHexString(publicKeyHex);
            var sigBytes       = Convert.FromHexString(signatureHex);
            if (!PublicKey.TryImport(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey, out var pk))
                return false;
            var message = Encoding.UTF8.GetBytes(timestamp).Concat(body).ToArray();
            return algorithm.Verify(pk!, message, sigBytes);
        }
        catch { return false; }
    }
}
