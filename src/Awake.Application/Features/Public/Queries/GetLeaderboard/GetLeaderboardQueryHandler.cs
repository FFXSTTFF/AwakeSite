using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Public.Queries.GetLeaderboard;

public class GetLeaderboardQueryHandler(
    IUserRepository userRepository,
    IPlayerStatsSnapshotRepository snapshotRepository
) : IRequestHandler<GetLeaderboardQuery, IReadOnlyList<LeaderboardEntryDto>>
{
    public async Task<IReadOnlyList<LeaderboardEntryDto>> Handle(
        GetLeaderboardQuery request, CancellationToken cancellationToken)
    {
        var members = await userRepository.GetByMinRankAsync(UserRank.Member, cancellationToken);
        var nicknames = members
            .Where(u => !string.IsNullOrEmpty(u.GameNickname))
            .Select(u => u.GameNickname!)
            .Distinct()
            .ToList();

        if (nicknames.Count == 0)
            return [];

        var snapshots = await snapshotRepository.GetByNicknamesAsync(nicknames, cancellationToken);

        return snapshots
            .OrderByDescending(s => s.Kills)
            .Take(request.Count)
            .Select(s => new LeaderboardEntryDto(s.GameNickname, s.Kills, s.Accuracy, s.Playtime))
            .ToList();
    }
}
