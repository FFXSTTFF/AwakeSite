using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Players.Queries.GetPlayerProfile;

public class GetPlayerProfileQueryHandler(
    IUserRepository userRepository,
    ISquadRepository squadRepository,
    ITicketRepository ticketRepository,
    IPlayerStatsSnapshotRepository snapshotRepository,
    IPlayerBoostRequestRepository boostRepository
) : IRequestHandler<GetPlayerProfileQuery, Result<PlayerProfileDto>>
{
    public async Task<Result<PlayerProfileDto>> Handle(
        GetPlayerProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result<PlayerProfileDto>.Failure("Пользователь не найден.");

        var membership = await squadRepository
            .GetMembershipByUserIdAsync(user.Id, cancellationToken);
        var squad = membership is null
            ? null
            : new PlayerSquadDto(membership.SquadId, membership.Squad.Name,
                membership.Squad.Number, membership.IsLeader);

        PlayerStatsDto? stats = null;
        if (!string.IsNullOrEmpty(user.GameNickname))
        {
            var snapshot = await snapshotRepository
                .GetByNicknameAsync(user.GameNickname, cancellationToken);
            if (snapshot is not null)
            {
                stats = new PlayerStatsDto(
                    snapshot.Kills, snapshot.Deaths, snapshot.KdRatio,
                    snapshot.Accuracy, snapshot.Playtime,
                    snapshot.ClanHistory, snapshot.FetchedAt);
            }
        }

        // Экипировка: сохранённая в профиле, иначе — из самой свежей заявки с Loadout
        var loadout = user.Loadout;
        if (loadout is null)
        {
            var tickets = await ticketRepository.GetByAuthorAsync(user.Id, cancellationToken);
            loadout = tickets.FirstOrDefault(t => t.Loadout is not null)?.Loadout;
        }

        var boosts = await boostRepository.GetByUserIdAsync(user.Id, cancellationToken);

        return Result<PlayerProfileDto>.Success(new PlayerProfileDto(
            user.Id, user.Username, user.DiscordUsername, user.DiscordAvatarUrl,
            user.Rank, user.GameNickname, squad, stats, loadout, boosts));
    }
}
