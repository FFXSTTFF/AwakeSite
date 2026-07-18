using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Squads.Queries.GetSquadById;
using Awake.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Squads;

public class GetSquadByIdQueryHandlerTests
{
    private readonly Mock<ISquadRepository> _squads = new();
    private readonly Mock<IPlayerInventoryRepository> _inventory = new();
    private readonly Mock<IPlayerBuildProofRepository> _proofs = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private readonly Mock<IPlayerStatsSnapshotRepository> _snapshots = new();
    private readonly Guid _squadId = Guid.NewGuid();

    private GetSquadByIdQueryHandler BuildHandler() => new(
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
    public async Task Handle_ReturnsMemberWithFlags()
    {
        var user = new User { Username = "u1" };
        var squad = new Squad
        {
            Id = _squadId, Name = "Alpha", Number = 1,
            Members = [new SquadMember { UserId = user.Id, User = user }],
        };
        _squads.Setup(r => r.GetByIdWithMembersAsync(_squadId, It.IsAny<CancellationToken>())).ReturnsAsync(squad);
        SetupEmptyAux();

        var result = await BuildHandler().Handle(new GetSquadByIdQuery(_squadId), CancellationToken.None);

        result.Members.Should().ContainSingle().Which.Flags.Should().NotBeNull();
    }
}
