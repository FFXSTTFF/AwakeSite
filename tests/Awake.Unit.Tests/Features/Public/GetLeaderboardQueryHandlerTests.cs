using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Public.Queries.GetLeaderboard;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Public;

public class GetLeaderboardQueryHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPlayerStatsSnapshotRepository> _snapshots = new();

    private GetLeaderboardQueryHandler CreateHandler()
        => new(_users.Object, _snapshots.Object);

    private static User Member(string nickname) => new()
    {
        Id = Guid.NewGuid(), Username = nickname,
        Rank = UserRank.Member, GameNickname = nickname
    };

    private static PlayerStatsSnapshot Snapshot(string nickname, int kills) => new()
    {
        Id = Guid.NewGuid(), GameNickname = nickname, Kills = kills,
        Accuracy = "50%", Playtime = "100 ч.", FetchedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Handle_SortsByKillsDescending_AndLimitsToCount()
    {
        var users = Enumerable.Range(1, 12).Select(i => Member($"player{i}")).ToList();
        var snapshots = Enumerable.Range(1, 12)
            .Select(i => Snapshot($"player{i}", kills: i * 100)).ToList();

        _users.Setup(r => r.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync(users);
        _snapshots.Setup(r => r.GetByNicknamesAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(snapshots);

        var result = await CreateHandler().Handle(new GetLeaderboardQuery(), CancellationToken.None);

        result.Should().HaveCount(10);
        result[0].GameNickname.Should().Be("player12");
        result[0].Kills.Should().Be(1200);
        result.Select(e => e.Kills).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Handle_SkipsUsersWithoutNickname()
    {
        var withNick = Member("sniper");
        var withoutNick = new User
        {
            Id = Guid.NewGuid(), Username = "ghost",
            Rank = UserRank.Member, GameNickname = null
        };

        _users.Setup(r => r.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([withNick, withoutNick]);
        _snapshots.Setup(r => r.GetByNicknamesAsync(
                It.Is<IReadOnlyCollection<string>>(n => n.Count == 1 && n.Contains("sniper")),
                It.IsAny<CancellationToken>()))
              .ReturnsAsync([Snapshot("sniper", 500)]);

        var result = await CreateHandler().Handle(new GetLeaderboardQuery(), CancellationToken.None);

        result.Should().ContainSingle(e => e.GameNickname == "sniper");
    }

    [Fact]
    public async Task Handle_NoMembersWithNickname_ReturnsEmpty_WithoutQueryingSnapshots()
    {
        _users.Setup(r => r.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([]);

        var result = await CreateHandler().Handle(new GetLeaderboardQuery(), CancellationToken.None);

        result.Should().BeEmpty();
        _snapshots.Verify(r => r.GetByNicknamesAsync(
            It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
