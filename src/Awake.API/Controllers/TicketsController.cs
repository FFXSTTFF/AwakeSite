using Awake.API.Filters;
using Awake.Application.Features.Tickets.Commands.AddTicketComment;
using Awake.Application.Features.Tickets.Commands.CreateTicket;
using Awake.Application.Features.Tickets.Commands.UpdateTicketStatus;
using Awake.Application.Features.Tickets.Queries.GetTicketById;
using Awake.Application.Features.Tickets.Queries.GetTickets;
using Awake.Domain.Enums;
using Awake.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Awake.API.Controllers;

public record CreateTicketRequest(string GameNickname, TicketType Type, string Description, Loadout? Loadout);
public record UpdateStatusRequest(TicketStatus NewStatus);
public record AddCommentRequest(string Content);

[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await sender.Send(new GetTicketsQuery(), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTicketRequest request, CancellationToken ct)
    {
        var command = new CreateTicketCommand(request.GameNickname, request.Type, request.Description, request.Loadout);
        var result = await sender.Send(command, ct);
        return result.IsSuccess
            ? Created(string.Empty, result.Value)
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetTicketByIdQuery(id), ct);
        if (!result.IsSuccess)
            return result.Error == "Тикет не найден."
                ? NotFound()
                : Problem(detail: result.Error, statusCode: StatusCodes.Status403Forbidden);
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/status")]
    [RankAuthorize(UserRank.Officer)]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateStatusRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateTicketStatusCommand(id, request.NewStatus), ct);
        return result.IsSuccess
            ? NoContent()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status404NotFound);
    }

    [HttpPost("{id:guid}/comments")]
    [RankAuthorize(UserRank.Officer)]
    public async Task<IActionResult> AddComment(Guid id, AddCommentRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new AddTicketCommentCommand(id, request.Content), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(detail: result.Error, statusCode: StatusCodes.Status404NotFound);
    }
}
