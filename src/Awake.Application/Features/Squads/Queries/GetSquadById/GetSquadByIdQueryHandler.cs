using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Squads.Queries.GetSquads;
using Awake.Domain.Entities;
using Awake.Domain.Exceptions;
using MediatR;

namespace Awake.Application.Features.Squads.Queries.GetSquadById;

public class GetSquadByIdQueryHandler(ISquadRepository squadRepository)
    : IRequestHandler<GetSquadByIdQuery, SquadDto>
{
    public async Task<SquadDto> Handle(
        GetSquadByIdQuery request,
        CancellationToken cancellationToken)
    {
        var squad = await squadRepository.GetByIdWithMembersAsync(request.SquadId, cancellationToken)
            ?? throw new NotFoundException(nameof(Squad), request.SquadId);

        return new SquadDto(
            squad.Id,
            squad.Name,
            squad.Number,
            squad.Members
                .OrderByDescending(m => m.IsLeader)
                .ThenBy(m => m.JoinedAt)
                .Select(m => new SquadMemberDto(
                    m.UserId,
                    m.User.Username,
                    m.User.GameNickname,
                    m.IsLeader,
                    m.JoinedAt))
                .ToList(),
            squad.Members.Count);
    }
}
