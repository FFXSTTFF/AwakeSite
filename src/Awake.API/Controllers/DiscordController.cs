using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Awake.Application.Features.Tickets.Commands.CreateDiscordTicket;
using Awake.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSec.Cryptography;

namespace Awake.API.Controllers;

[ApiController]
[Route("api/discord")]
public class DiscordController(IMediator mediator, IConfiguration configuration) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [HttpPost("interactions")]
    public async Task<IActionResult> Interactions()
    {
        // Read raw body for signature verification
        Request.EnableBuffering();
        var bodyBytes = await ReadBodyAsync();
        Request.Body.Position = 0;

        // Verify Ed25519 signature
        var publicKeyHex = configuration["Discord:PublicKey"] ?? string.Empty;
        var timestamp = Request.Headers["X-Signature-Timestamp"].ToString();
        var signature = Request.Headers["X-Signature-Ed25519"].ToString();

        if (!VerifySignature(publicKeyHex, timestamp, signature, bodyBytes))
            return Unauthorized();

        using var doc = JsonDocument.Parse(bodyBytes);
        var root = doc.RootElement;
        var type = root.GetProperty("type").GetInt32();

        // PING
        if (type == 1)
            return Ok(new { type = 1 });

        // APPLICATION_COMMAND — show modal
        if (type == 2)
        {
            var commandName = root.GetProperty("data").GetProperty("name").GetString();
            if (commandName is "заявка" or "ticket" or "apply")
                return Ok(BuildModal());
        }

        // MODAL_SUBMIT — create ticket
        if (type == 5)
        {
            var customId = root.GetProperty("data").GetProperty("custom_id").GetString();
            if (customId == "ticket_modal")
            {
                var userId = root.GetProperty("member")
                    .TryGetProperty("user", out var memberUser)
                    ? memberUser.GetProperty("id").GetString()!
                    : root.GetProperty("user").GetProperty("id").GetString()!;

                var username = root.GetProperty("member")
                    .TryGetProperty("user", out var mu)
                    ? (mu.GetProperty("global_name").GetString() ?? mu.GetProperty("username").GetString())!
                    : (root.GetProperty("user").GetProperty("global_name").GetString()
                       ?? root.GetProperty("user").GetProperty("username").GetString())!;

                var components = root.GetProperty("data").GetProperty("components");
                var values = ExtractModalValues(components);

                values.TryGetValue("game_nickname", out var nickname);
                values.TryGetValue("description", out var description);

                if (!string.IsNullOrWhiteSpace(nickname) && !string.IsNullOrWhiteSpace(description))
                {
                    await mediator.Send(new CreateDiscordTicketCommand(
                        userId, username, nickname, TicketType.Recruitment, description));
                }

                return Ok(new
                {
                    type = 4,
                    data = new
                    {
                        content = "✅ Заявка отправлена! Офицеры рассмотрят её в ближайшее время.",
                        flags = 64 // EPHEMERAL
                    }
                });
            }
        }

        return Ok(new { type = 1 });
    }

    /// <summary>Register slash command with Discord (run once via admin call).</summary>
    [HttpPost("register-commands")]
    public async Task<IActionResult> RegisterCommands()
    {
        var botToken = configuration["Discord:BotToken"];
        var appId = configuration["Discord:ApplicationId"];
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(appId))
            return BadRequest("Discord:BotToken and Discord:ApplicationId must be configured.");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bot {botToken}");

        var command = new
        {
            name = "заявка",
            type = 1,
            description = "Подать заявку на вступление в клан Awake [LOVE]",
        };

        var resp = await http.PostAsJsonAsync(
            $"https://discord.com/api/v10/applications/{appId}/commands", command);

        return resp.IsSuccessStatusCode
            ? Ok("Slash command registered.")
            : StatusCode((int)resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static object BuildModal() => new
    {
        type = 9,
        data = new
        {
            title = "Заявка в клан Awake [LOVE]",
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
                            label = "Игровой никнейм в STALCRAFT",
                            style = 1,
                            required = true,
                            max_length = 100,
                            placeholder = "Твой никнейм"
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
                            label = "Расскажи о себе",
                            style = 2,
                            required = true,
                            max_length = 2000,
                            placeholder = "Опыт, достижения, почему хочешь в клан..."
                        }
                    }
                }
            }
        }
    };

    private static Dictionary<string, string> ExtractModalValues(JsonElement components)
    {
        var result = new Dictionary<string, string>();
        foreach (var row in components.EnumerateArray())
        {
            foreach (var comp in row.GetProperty("components").EnumerateArray())
            {
                var id = comp.GetProperty("custom_id").GetString()!;
                var value = comp.GetProperty("value").GetString() ?? string.Empty;
                result[id] = value;
            }
        }
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
            var algorithm = SignatureAlgorithm.Ed25519;
            var publicKeyBytes = Convert.FromHexString(publicKeyHex);
            var signatureBytes = Convert.FromHexString(signatureHex);

            if (!PublicKey.TryImport(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey, out var publicKey))
                return false;

            var message = Encoding.UTF8.GetBytes(timestamp).Concat(body).ToArray();
            return algorithm.Verify(publicKey!, message, signatureBytes);
        }
        catch
        {
            return false;
        }
    }
}
