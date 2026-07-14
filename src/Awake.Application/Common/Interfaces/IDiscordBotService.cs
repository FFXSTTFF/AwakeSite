using Awake.Domain.ValueObjects;

namespace Awake.Application.Common.Interfaces;

public interface IDiscordBotService
{
    Task SendDmAsync(string discordUserId, string message, CancellationToken ct = default);

    Task PostApplicationButtonAsync(string channelId, CancellationToken ct = default);

    Task<string?> GetChannelParentIdAsync(string channelId, CancellationToken ct = default);

    Task<string?> CreateTicketChannelAsync(
        string guildId,
        string categoryId,
        string userId,
        string username,
        string? adminRoleId,
        string gameNickname,
        CancellationToken ct = default);

    Task PostTicketEmbedAsync(
        string channelId,
        Guid ticketId,
        string gameNickname,
        string description,
        string discordUsername,
        Loadout? loadout = null,
        PlayerProfile? playerProfile = null,
        CancellationToken ct = default);

    Task PostAdminEmbedAsync(
        string adminChannelId,
        string ticketChannelId,
        string gameNickname,
        string discordUsername,
        CancellationToken ct = default);

    Task PostStatusUpdateAsync(string channelId, string statusText, CancellationToken ct = default);

    Task DeleteChannelAsync(string channelId, CancellationToken ct = default);

    Task FollowUpAsync(string applicationId, string interactionToken, string content, CancellationToken ct = default);

    Task PostCommentAsync(string channelId, string authorUsername, string content, CancellationToken ct = default);
    Task PostMessageAsync(string channelId, string content, CancellationToken ct = default);

    /// <summary>ID ролей участника сервера; null — не участник или запрос не удался.</summary>
    Task<IReadOnlyCollection<string>?> GetGuildMemberRoleIdsAsync(
        string guildId, string discordUserId, CancellationToken ct = default);
}
