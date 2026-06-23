using Awake.Application.Features.Notifications.Commands.MarkRead;
using Awake.Application.Features.Notifications.Queries.GetNotifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Awake.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await mediator.Send(new GetNotificationsQuery(), ct);
        return Ok(result.Value);
    }

    [HttpPut("read")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await mediator.Send(new MarkNotificationsReadCommand(), ct);
        return NoContent();
    }
}
