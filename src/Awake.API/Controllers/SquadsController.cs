using Awake.API.Filters;
using Awake.Application.Features.Squads.Commands.AddMember;
using Awake.Application.Features.Squads.Commands.RemoveMember;
using Awake.Application.Features.Squads.Commands.SetLeader;
using Awake.Application.Features.Squads.Queries.GetSquadById;
using Awake.Application.Features.Squads.Queries.GetSquads;
using Awake.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Awake.API.Controllers;

public record AddMemberRequest(Guid UserId);
public record SetLeaderRequest(Guid UserId);

[ApiController]
[Route("api/squads")]
[Authorize]
public class SquadsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await sender.Send(new GetSquadsQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
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
}
