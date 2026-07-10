namespace Awake.Application.Common.Interfaces;

public record DiscordUserInfo(string Id, string Username, string? GlobalName, string? AvatarUrl);

public interface IDiscordOAuthService
{
    string GetAuthorizationUrl(string state);
    Task<DiscordUserInfo?> ExchangeCodeAsync(string code, CancellationToken ct = default);
}
