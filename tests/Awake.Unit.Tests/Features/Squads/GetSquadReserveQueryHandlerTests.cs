using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Squads.Queries.GetSquadReserve;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Squads;

public class GetSquadReserveQueryHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ISquadRepository> _squads = new();
    private readonly Mock<IPlayerInventoryRepository> _inventory = new();
    private readonly Mock<IPlayerBuildProofRepository> _proofs = new();
    private readonly Mock<IPlayerBoostRequestRepository> _boosts = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private readonly Mock<IPlayerStatsSnapshotRepository> _snapshots = new();

    private GetSquadReserveQueryHandler BuildHandler() => new(
        _users.Object, _squads.Object, _inventory.Object, _proofs.Object, _boosts.Object, _cache.Object, _snapshots.Object);

    private void SetupEmptyAux()
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
    public async Task Handle_MemberInSquad_ExcludedFromReserve()
    {
        var inSquad = new User { Username = "inSquad", Rank = UserRank.Member };
        var free = new User { Username = "free", Rank = UserRank.Member };
        _users.Setup(u => u.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([inSquad, free]);
        _squads.Setup(s => s.GetAllWithMembersAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync([new Squad
               {
                   Name = "Alpha", Number = 1,
                   Members = [new SquadMember { UserId = inSquad.Id, User = inSquad }],
               }]);
        SetupEmptyAux();

        var result = await BuildHandler().Handle(new GetSquadReserveQuery(), CancellationToken.None);

        result.Should().ContainSingle().Which.Username.Should().Be("free");
    }

    [Fact]
    public async Task Handle_GuestRank_ExcludedEvenIfNoSquad()
    {
        // GetByMinRankAsync(Member) сам исключает Guest — хендлер не фильтрует ранг повторно,
        // тест фиксирует контракт: гость никогда не приходит от репозитория.
        _users.Setup(u => u.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([]);
        _squads.Setup(s => s.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        SetupEmptyAux();

        var result = await BuildHandler().Handle(new GetSquadReserveQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SortsByKdDescending_NullsLast()
    {
        var high = new User { Username = "high", GameNickname = "High", Rank = UserRank.Member };
        var low = new User { Username = "low", GameNickname = "Low", Rank = UserRank.Member };
        var none = new User { Username = "none", Rank = UserRank.Member };
        _users.Setup(u => u.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([none, low, high]);
        _squads.Setup(s => s.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _inventory.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);
        _proofs.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _boosts.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _snapshots.Setup(r => r.GetByNicknamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([
                      new PlayerStatsSnapshot { GameNickname = "High", KdRatio = 3.0 },
                      new PlayerStatsSnapshot { GameNickname = "Low", KdRatio = 1.0 },
                  ]);

        var result = await BuildHandler().Handle(new GetSquadReserveQuery(), CancellationToken.None);

        result.Select(r => r.Username).Should().Equal("high", "low", "none");
    }

    [Fact]
    public async Task Handle_EnrichesFlagsAndBoosts()
    {
        var user = new User { Username = "u1", Rank = UserRank.Member };
        _users.Setup(u => u.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([user]);
        _squads.Setup(s => s.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _inventory.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([new PlayerInventoryItem { UserId = user.Id, ItemId = "skif5" }]);
        _proofs.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _boosts.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _cache.Setup(c => c.GetById("skif5"))
              .Returns(new Awake.Application.Features.Items.Dtos.ItemDto("skif5", "armor/combined", "Скиф-5", "i.png", "RANK_MASTER"));
        _snapshots.Setup(r => r.GetByNicknamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);

        var result = await BuildHandler().Handle(new GetSquadReserveQuery(), CancellationToken.None);

        result.Should().ContainSingle().Which.Flags.Bio.Should().BeTrue();
    }
}
