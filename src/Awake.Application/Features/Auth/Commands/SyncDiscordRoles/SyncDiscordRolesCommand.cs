using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Auth.Commands.SyncDiscordRoles;

/// <summary>
/// Приводит ранг пользователя сайта к его ролям Discord.
/// RoleIds == null — роли запрашиваются у Discord REST (сверка при логине);
/// иначе используется переданный список (событие Gateway).
/// Возвращает true, если ранг был изменён.
/// </summary>
public record SyncDiscordRolesCommand(
    string DiscordUserId,
    IReadOnlyCollection<string>? RoleIds = null
) : IRequest<Result<bool>>;
