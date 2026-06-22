using Awake.Application.Common.Interfaces.Repositories;
using MediatR;

namespace Awake.Application.Features.Squads.Queries.GetSquads;

public class GetSquadsQueryHandler(ISquadRepository squadRepository)
    : IRequestHandler<GetSquadsQuery, IReadOnlyList<SquadDto>>
{
    public async Task<IReadOnlyList<SquadDto>> Handle(
        GetSquadsQuery request,
        CancellationToken cancellationToken)
    {
        var squads = await squadRepository.GetAllWithMembersAsync(cancellationToken);
        return squads
            .OrderBy(s => s.Number)
            .Select(s => new SquadDto(
                s.Id,
                s.Name,
                s.Number,
                s.Members
                    .OrderByDescending(m => m.IsLeader)
                    .ThenBy(m => m.JoinedAt)
                    .Select(m => new SquadMemberDto(
                        m.UserId,
                        m.User.Username,
                        m.User.GameNickname,
                        m.IsLeader,
                        m.JoinedAt))
                    .ToList(),
                s.Members.Count))
            .ToList();
    }
}
