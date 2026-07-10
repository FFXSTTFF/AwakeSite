using System.Net.Http.Json;
using System.Text.Json;
using Awake.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Awake.Infrastructure.ExternalServices.Discord;

// Discord OAuth2 authorization code flow. Client secret никогда не покидает сервер.
public class DiscordOAuthService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<DiscordOAuthService> logger) : IDiscordOAuthService
{
    private const string ApiBase = "https://discord.com/api/v10";

    public string GetAuthorizationUrl(string state)
    {
        var clientId = configuration["Discord:ApplicationId"];
        var redirectUri = configuration["Discord:OAuthRedirectUri"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
        {
            logger.LogWarning("Discord:ApplicationId or Discord:OAuthRedirectUri is not configured.");
            throw new InvalidOperationException("Discord:OAuthRedirectUri is not configured.");
        }

        return $"https://discord.com/oauth2/authorize?client_id={clientId}" +
               $"&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&scope=identify&state={Uri.EscapeDataString(state)}";
    }

    public async Task<DiscordUserInfo?> ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        try
        {
            var tokenResp = await httpClient.PostAsync($"{ApiBase}/oauth2/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = configuration["Discord:ApplicationId"] ?? "",
                    ["client_secret"] = configuration["Discord:ClientSecret"] ?? "",
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = configuration["Discord:OAuthRedirectUri"] ?? "",
                }), ct);

            if (!tokenResp.IsSuccessStatusCode)
            {
                logger.LogWarning("Discord token exchange failed: {Status}", tokenResp.StatusCode);
                return null;
            }

            var tokenJson = await tokenResp.Content.ReadFromJsonAsync<JsonElement>(ct);
            var accessToken = tokenJson.GetProperty("access_token").GetString();

            using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/users/@me");
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
            var userResp = await httpClient.SendAsync(req, ct);

            if (!userResp.IsSuccessStatusCode)
            {
                logger.LogWarning("Discord users/@me failed: {Status}", userResp.StatusCode);
                return null;
            }

            var userJson = await userResp.Content.ReadFromJsonAsync<JsonElement>(ct);
            return ParseUser(userJson);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Discord OAuth exchange failed");
            return null;
        }
    }

    internal static DiscordUserInfo ParseUser(JsonElement json)
    {
        var id = json.GetProperty("id").GetString() ?? "";
        var username = json.GetProperty("username").GetString() ?? "";
        var globalName = json.TryGetProperty("global_name", out var gn) && gn.ValueKind == JsonValueKind.String
            ? gn.GetString() : null;
        var avatarHash = json.TryGetProperty("avatar", out var av) && av.ValueKind == JsonValueKind.String
            ? av.GetString() : null;
        var avatarUrl = avatarHash is null
            ? null
            : $"https://cdn.discordapp.com/avatars/{id}/{avatarHash}.png";
        return new DiscordUserInfo(id, username, globalName, avatarUrl);
    }
}
