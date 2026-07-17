using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Items.Dtos;
using Awake.Application.Features.Squads.Queries.GetSquadBuilder;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Squads;

public class GetSquadBuilderQueryHandlerTests
{
    private readonly Mock<ISquadRepository> _squads = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPlayerInventoryRepository> _inventory = new();
    private readonly Mock<IPlayerBuildProofRepository> _proofs = new();
    private readonly Mock<IPlayerBoostRequestRepository> _boosts = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private readonly Mock<IPlayerStatsSnapshotRepository> _snapshots = new();

    private GetSquadBuilderQueryHandler BuildHandler() => new(
        _squads.Object, _users.Object, _inventory.Object,
        _proofs.Object, _boosts.Object, _cache.Object, _snapshots.Object);

    private static User MakeUser(UserRank rank = UserRank.Member, string? nickname = null) =>
        new() { Username = "u_" + Guid.NewGuid().ToString("N")[..6], Rank = rank, GameNickname = nickname };

    private void SetupEmptyAux(params Guid[] ids)
    {
        _inventory.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);
        _proofs.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _boosts.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _snapshots.Setup(r => r.GetByNicknamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);
    }

    [Fact]
    public async Task Handle_SplitsUsersIntoSquadsAndPool()
    {
        var inSquad = MakeUser();
        var free = MakeUser();
        var squad = new Squad
        {
            Name = "Alpha", Number = 1,
            Members = [new SquadMember { UserId = inSquad.Id, User = inSquad }],
        };
        _squads.Setup(r => r.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([squad]);
        _users.Setup(r => r.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([inSquad, free]);
        SetupEmptyAux();

        var result = await BuildHandler().Handle(new GetSquadBuilderQuery(), CancellationToken.None);

        var dto = result.Value!;
        dto.Squads.Should().ContainSingle().Which.Members
            .Should().ContainSingle().Which.UserId.Should().Be(inSquad.Id);
        dto.Pool.Should().ContainSingle().Which.UserId.Should().Be(free.Id);
    }

    [Fact]
    public async Task Handle_FlagsAndKd_ComputedPerFighter()
    {
        var user = MakeUser(nickname: "Yap");
        _squads.Setup(r => r.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _users.Setup(r => r.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([user]);
        _inventory.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([new PlayerInventoryItem { UserId = user.Id, ItemId = "skif5" }]);
        _proofs.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([new PlayerBuildProof { UserId = user.Id, BuildType = BuildType.Speed }]);
        _boosts.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _cache.Setup(c => c.GetById("skif5"))
              .Returns(new ItemDto("skif5", "armor/combined", "Скиф-5", "i.png", "RANK_MASTER"));
        _snapshots.Setup(r => r.GetByNicknamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([new PlayerStatsSnapshot { GameNickname = "Yap", KdRatio = 2.5 }]);

        var result = await BuildHandler().Handle(new GetSquadBuilderQuery(), CancellationToken.None);

        var fighter = result.Value!.Pool.Should().ContainSingle().Subject;
        fighter.Flags.Bio.Should().BeTrue();
        fighter.Flags.Speed.Should().BeTrue();
        fighter.Flags.Combat.Should().BeFalse();
        fighter.Kd.Should().Be(2.5);
    }

    [Fact]
    public async Task Handle_NoNicknameOrSnapshot_KdNull()
    {
        var noNick = MakeUser();
        var noSnap = MakeUser(nickname: "Ghost");
        _squads.Setup(r => r.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _users.Setup(r => r.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([noNick, noSnap]);
        SetupEmptyAux();

        var result = await BuildHandler().Handle(new GetSquadBuilderQuery(), CancellationToken.None);

        result.Value!.Pool.Should().HaveCount(2)
            .And.OnlyContain(f => f.Kd == null);
    }

    [Fact]
    public async Task Handle_GuestNotInPool()
    {
        // GetByMinRankAsync(Member) по контракту не возвращает гостей —
        // но участник отряда с рангом Guest (понижен после добавления) не должен попасть в пул
        var demoted = MakeUser(rank: UserRank.Guest);
        var squad = new Squad
        {
            Name = "Alpha", Number = 1,
            Members = [new SquadMember { UserId = demoted.Id, User = demoted }],
        };
        _squads.Setup(r => r.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([squad]);
        _users.Setup(r => r.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([]);
        SetupEmptyAux();

        var result = await BuildHandler().Handle(new GetSquadBuilderQuery(), CancellationToken.None);

        result.Value!.Squads.Should().ContainSingle().Which.Members.Should().HaveCount(1);
        result.Value.Pool.Should().BeEmpty();
    }
}
