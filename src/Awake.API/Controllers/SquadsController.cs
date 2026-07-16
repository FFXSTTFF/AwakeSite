using Awake.API.Filters;
using Awake.Application.Features.Squads.Commands.AddMember;
using Awake.Application.Features.Squads.Commands.MoveMember;
using Awake.Application.Features.Squads.Commands.RemoveMember;
using Awake.Application.Features.Squads.Commands.RenameSquad;
using Awake.Application.Features.Squads.Commands.SetLeader;
using Awake.Application.Features.Squads.Queries.GetSquadBuilder;
using Awake.Application.Features.Squads.Queries.GetSquadById;
using Awake.Application.Features.Squads.Queries.GetSquads;
using Awake.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Awake.API.Controllers;

public record AddMemberRequest(Guid UserId);
public record SetLeaderRequest(Guid UserId);
public record MoveMemberRequest(Guid UserId);
public record RenameSquadRequest(string Name);

[ApiController]
[Route("api/squads")]
[Authorize]
public class SquadsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RankAuthorize(UserRank.Member)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await sender.Send(new GetSquadsQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RankAuthorize(UserRank.Member)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetSquadByIdQuery(id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/members")]
    [RankAuthorize(UserRank.Colonel)]
    public async Task<IActionResult> AddMember(Guid id, AddMemberRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new AddSquadMemberCommand(id, request.UserId), ct);
        return result.IsSuccess
            ? NoContent()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [RankAuthorize(UserRank.Colonel)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveSquadMemberCommand(id, userId), ct);
        return result.IsSuccess
            ? NoContent()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    [HttpPut("{id:guid}/leader")]
    [RankAuthorize(UserRank.Colonel)]
    public async Task<IActionResult> SetLeader(Guid id, SetLeaderRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new SetSquadLeaderCommand(id, request.UserId), ct);
        return result.IsSuccess
            ? NoContent()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    [HttpPut("{id:guid}/name")]
    [RankAuthorize(UserRank.Officer)]
    public async Task<IActionResult> Rename(Guid id, RenameSquadRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new RenameSquadCommand(id, request.Name), ct);
        return result.IsSuccess
            ? NoContent()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    // ── Билдер отрядов (Officer+, спека этапа 2) ────────────────────────────

    [HttpGet("builder")]
    [RankAuthorize(UserRank.Officer)]
    public async Task<IActionResult> GetBuilder(CancellationToken ct)
    {
        var result = await sender.Send(new GetSquadBuilderQuery(), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    [HttpPost("{id:guid}/move-member")]
    [RankAuthorize(UserRank.Officer)]
    public async Task<IActionResult> MoveMember(Guid id, MoveMemberRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new MoveSquadMemberCommand(id, request.UserId), ct);
        return result.IsSuccess
            ? NoContent()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    // Удаление в пул из билдера: существующий DELETE members остаётся Colonel+,
    // билдер по спеке редактируют Officer+ — отдельный маршрут с тем же хэндлером
    [HttpDelete("{id:guid}/builder-members/{userId:guid}")]
    [RankAuthorize(UserRank.Officer)]
    public async Task<IActionResult> RemoveMemberFromBuilder(Guid id, Guid userId, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveSquadMemberCommand(id, userId), ct);
        return result.IsSuccess
            ? NoContent()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }
}
