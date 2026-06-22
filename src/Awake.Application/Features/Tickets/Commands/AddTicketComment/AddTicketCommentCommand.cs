using Awake.Application.Common.Models;
using Awake.Application.Features.Tickets.Dtos;
using MediatR;

namespace Awake.Application.Features.Tickets.Commands.AddTicketComment;

public record AddTicketCommentCommand(Guid TicketId, string Content) : IRequest<Result<TicketCommentDto>>;
