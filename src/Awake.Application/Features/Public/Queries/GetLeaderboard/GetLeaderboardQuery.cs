using MediatR;

namespace Awake.Application.Features.Public.Queries.GetLeaderboard;

public record GetLeaderboardQuery(int Count = 10) : IRequest<IReadOnlyList<LeaderboardEntryDto>>;
