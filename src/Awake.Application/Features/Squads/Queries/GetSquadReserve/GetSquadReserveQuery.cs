using MediatR;

namespace Awake.Application.Features.Squads.Queries.GetSquadReserve;

public record GetSquadReserveQuery : IRequest<IReadOnlyList<ReserveMemberDto>>;
