using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Tickets.Commands.AddTicketComment;

public record AddDiscordCommentCommand(
    Guid TicketId,
    string DiscordUsername,
    string Content
) : IRequest<Result<bool>>;
