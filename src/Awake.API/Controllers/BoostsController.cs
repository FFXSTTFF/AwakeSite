using Awake.API.Filters;
using Awake.Application.Common.Interfaces;
using Awake.Application.Features.Boosts.Commands.SetMyBoosts;
using Awake.Application.Features.Boosts.Queries.GetBoostsSummary;
using Awake.Application.Features.Boosts.Queries.GetMyBoosts;
using Awake.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Awake.API.Controllers;

public record SetBoostsRequest(IReadOnlyList<BoostType> BoostTypes);

[ApiController]
[Authorize]
public class BoostsController(
    ISender sender,
    ICurrentUserService currentUser
) : ControllerBase
{
    // ── Свои бусты (любой ранг — как остальные api/profile/*) ──────────────

    [HttpGet("api/profile/boosts")]
    public async Task<IActionResult> GetMy(CancellationToken ct) =>
        Ok(await sender.Send(new GetMyBoostsQuery(currentUser.UserId), ct));

    [HttpPut("api/profile/boosts")]
    public async Task<IActionResult> SetMy(SetBoostsRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new SetMyBoostsCommand(currentUser.UserId, request.BoostTypes), ct);
        return result.IsSuccess
            ? NoContent()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    // ── Сводка по клану (Member+) ───────────────────────────────────────────

    [HttpGet("api/boosts/summary")]
    [RankAuthorize(UserRank.Member)]
    public async Task<IActionResult> Summary(CancellationToken ct) =>
        Ok(await sender.Send(new GetBoostsSummaryQuery(), ct));
}
