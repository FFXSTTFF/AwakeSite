using Awake.API.Filters;
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Players.Queries.GetPlayerProfile;
using Awake.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Awake.API.Controllers;

[ApiController]
[Route("api/players")]
[Authorize]
public class PlayersController(
    ISender sender,
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IPlayerDataAggregator playerData
) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct)
    {
        var result = await sender.Send(new GetPlayerProfileQuery(currentUser.UserId), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(detail: result.Error, statusCode: StatusCodes.Status404NotFound);
    }

    [HttpGet("{userId:guid}")]
    [RankAuthorize(UserRank.Member)]
    public async Task<IActionResult> GetProfile(Guid userId, CancellationToken ct)
    {
        var result = await sender.Send(new GetPlayerProfileQuery(userId), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(detail: result.Error, statusCode: StatusCodes.Status404NotFound);
    }

    // 202 сразу; обновление в фоне (FlareSolverr — 15–30 c). 429 — кулдаун 10 минут.
    [HttpPost("me/stats/refresh")]
    public async Task<IActionResult> RefreshMyStats(CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(currentUser.UserId, ct);
        if (user is null || string.IsNullOrEmpty(user.GameNickname))
            return Problem(detail: "Игровой ник не привязан.", statusCode: StatusCodes.Status400BadRequest);

        var nickname = user.GameNickname;
        // Кулдаун проверяем через TryBeginForceRefresh-семантику: запускаем задачу,
        // но ответ отдаём сразу. ForceRefreshAsync вернёт false мгновенно при кулдауне.
        var refreshTask = playerData.ForceRefreshAsync(nickname, CancellationToken.None);
        var completedQuickly = await Task.WhenAny(refreshTask, Task.Delay(500, ct)) == refreshTask;
        if (completedQuickly && !await refreshTask)
            return Problem(detail: "Обновлять статистику можно не чаще раза в 10 минут.",
                statusCode: StatusCodes.Status429TooManyRequests);

        return Accepted();
    }
}
