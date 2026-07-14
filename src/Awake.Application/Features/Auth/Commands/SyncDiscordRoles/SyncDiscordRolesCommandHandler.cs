using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Auth.Commands.SyncDiscordRoles;

public class SyncDiscordRolesCommandHandler(
    IUserRepository userRepository,
    IDiscordRoleSyncSettings settings,
    IDiscordBotService discordBotService,
    INotificationService notificationService
) : IRequestHandler<SyncDiscordRolesCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        SyncDiscordRolesCommand request, CancellationToken cancellationToken)
    {
        if (!settings.Enabled || settings.RoleToRank.Count == 0)
            return Result<bool>.Success(false);

        var user = await userRepository.GetByDiscordUserIdAsync(request.DiscordUserId, cancellationToken);
        if (user is null)
            return Result<bool>.Success(false);

        // Лидер вне автосинка: модератор Discord не должен управлять владельцем платформы
        if (user.Rank == UserRank.Leader)
            return Result<bool>.Success(false);

        var roleIds = request.RoleIds;
        if (roleIds is null)
        {
            if (string.IsNullOrWhiteSpace(settings.GuildId))
                return Result<bool>.Success(false);

            roleIds = await discordBotService.GetGuildMemberRoleIdsAsync(
                settings.GuildId, request.DiscordUserId, cancellationToken);

            // null = не участник сервера или REST недоступен — ранг не трогаем
            if (roleIds is null)
                return Result<bool>.Success(false);
        }

        var target = UserRank.Guest;
        foreach (var roleId in roleIds)
            if (settings.RoleToRank.TryGetValue(roleId, out var rank) && rank > target)
                target = rank;

        // Страховка на случай кривого маппинга: синк не выдаёт Leader
        if (target > UserRank.Colonel)
            target = UserRank.Colonel;

        if (user.Rank == target)
            return Result<bool>.Success(false);

        var promoted = target > user.Rank;
        user.Rank = target;
        await userRepository.UpdateAsync(user, cancellationToken);

        if (promoted)
        {
            await notificationService.CreateAsync(
                user.Id,
                "Ранг обновлён",
                "Твой ранг на сайте обновлён в соответствии с ролями Discord.",
                cancellationToken);
        }

        return Result<bool>.Success(true);
    }
}
