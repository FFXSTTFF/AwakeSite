using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Items.Dtos;
using Awake.Application.Features.Squads.Queries.GetSquads;
using Awake.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Squads;

public class GetSquadsQueryHandlerTests
{
    private readonly Mock<ISquadRepository> _squads = new();
    private readonly Mock<IPlayerInventoryRepository> _inventory = new();
    private readonly Mock<IPlayerBuildProofRepository> _proofs = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private readonly Mock<IPlayerStatsSnapshotRepository> _snapshots = new();

    private GetSquadsQueryHandler BuildHandler() => new(
        _squads.Object, _inventory.Object, _proofs.Object, _cache.Object, _snapshots.Object);

    private void SetupEmptyAux()
    {
        _inventory.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);
        _proofs.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _snapshots.Setup(r => r.GetByNicknamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);
    }

    [Fact]
    public async Task Handle_ReturnsMembersWithFlagsAndKd()
    {
        var user = new User { Username = "u1", GameNickname = "Yap" };
        var squad = new Squad
        {
            Name = "Alpha", Number = 2,
            Members = [new SquadMember { UserId = user.Id, User = user, IsLeader = true }],
        };
        _squads.Setup(r => r.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([squad]);
        _inventory.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([new PlayerInventoryItem { UserId = user.Id, ItemId = "skif5" }]);
        _proofs.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _cache.Setup(c => c.GetById("skif5"))
              .Returns(new ItemDto("skif5", "armor/combined", "Скиф-5", "i.png", "RANK_MASTER"));
        _snapshots.Setup(r => r.GetByNicknamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([new PlayerStatsSnapshot { GameNickname = "Yap", KdRatio = 1.8 }]);

        var result = await BuildHandler().Handle(new GetSquadsQuery(), CancellationToken.None);

        var member = result.Should().ContainSingle().Which.Members.Should().ContainSingle().Subject;
        member.Flags.Bio.Should().BeTrue();
        member.Kd.Should().Be(1.8);
    }

    [Fact]
    public async Task Handle_MemberWithoutNicknameOrSnapshot_KdNull()
    {
        var user = new User { Username = "u1" };
        var squad = new Squad
        {
            Name = "Alpha", Number = 1,
            Members = [new SquadMember { UserId = user.Id, User = user }],
        };
        _squads.Setup(r => r.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([squad]);
        SetupEmptyAux();

        var result = await BuildHandler().Handle(new GetSquadsQuery(), CancellationToken.None);

        result.Should().ContainSingle().Which.Members.Should().ContainSingle()
            .Which.Kd.Should().BeNull();
    }
}
