using Awake.Application.Common.Models;
using Awake.Application.Features.Tickets.Dtos;
using MediatR;

namespace Awake.Application.Features.Tickets.Queries.GetTicketById;

public record GetTicketByIdQuery(Guid TicketId) : IRequest<Result<TicketDetailDto>>;
