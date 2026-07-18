using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Items.Dtos;
using Awake.Application.Features.Squads;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Squads;

public class SquadMemberEnricherTests
{
    private readonly Mock<IPlayerInventoryRepository> _inventory = new();
    private readonly Mock<IPlayerBuildProofRepository> _proofs = new();
    private readonly Mock<IPlayerBoostRequestRepository> _boosts = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private readonly Mock<IPlayerStatsSnapshotRepository> _snapshots = new();

    private void SetupEmpty()
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
    public async Task ComputeAsync_FlagsAndKd_ComputedPerUser()
    {
        var user = new User { Username = "u1", Rank = UserRank.Member, GameNickname = "Yap" };
        _inventory.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([new PlayerInventoryItem { UserId = user.Id, ItemId = "skif5" }]);
        _proofs.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([new PlayerBuildProof { UserId = user.Id, BuildType = BuildType.Speed }]);
        _cache.Setup(c => c.GetById("skif5"))
              .Returns(new ItemDto("skif5", "armor/combined", "Скиф-5", "i.png", "RANK_MASTER"));
        _snapshots.Setup(r => r.GetByNicknamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([new PlayerStatsSnapshot { GameNickname = "Yap", KdRatio = 2.5 }]);
        _boosts.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);

        var result = await SquadMemberEnricher.ComputeAsync(
            [user], _inventory.Object, _proofs.Object, _boosts.Object, _cache.Object, _snapshots.Object, CancellationToken.None);

        result[user.Id].Flags.Bio.Should().BeTrue();
        result[user.Id].Flags.Speed.Should().BeTrue();
        result[user.Id].Flags.Combat.Should().BeFalse();
        result[user.Id].Kd.Should().Be(2.5);
    }

    [Fact]
    public async Task ComputeAsync_NoNicknameOrSnapshot_KdNull()
    {
        var noNick = new User { Username = "u1", Rank = UserRank.Member };
        var noSnap = new User { Username = "u2", Rank = UserRank.Member, GameNickname = "Ghost" };
        SetupEmpty();

        var result = await SquadMemberEnricher.ComputeAsync(
            [noNick, noSnap], _inventory.Object, _proofs.Object, _boosts.Object, _cache.Object, _snapshots.Object, CancellationToken.None);

        result[noNick.Id].Kd.Should().BeNull();
        result[noSnap.Id].Kd.Should().BeNull();
    }

    [Fact]
    public async Task ComputeAsync_BoostsGroupedPerUser()
    {
        var user = new User { Username = "u1", Rank = UserRank.Member };
        SetupEmpty();
        _boosts.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([
                   new PlayerBoostRequest { UserId = user.Id, BoostType = BoostType.Defense },
                   new PlayerBoostRequest { UserId = user.Id, BoostType = BoostType.Damage },
               ]);

        var result = await SquadMemberEnricher.ComputeAsync(
            [user], _inventory.Object, _proofs.Object, _boosts.Object, _cache.Object, _snapshots.Object, CancellationToken.None);

        result[user.Id].BoostTypes.Should().Equal(BoostType.Damage, BoostType.Defense); // отсортировано
    }
}
