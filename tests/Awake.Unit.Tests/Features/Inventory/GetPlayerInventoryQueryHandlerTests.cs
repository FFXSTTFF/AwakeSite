using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Inventory.Queries.GetPlayerInventory;
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Inventory;

public class GetPlayerInventoryQueryHandlerTests
{
    private readonly Mock<IPlayerInventoryRepository> _inventory = new();
    private readonly Mock<IPlayerBuildProofRepository> _proofs = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private readonly Guid _userId = Guid.NewGuid();

    private GetPlayerInventoryQueryHandler BuildHandler() =>
        new(_inventory.Object, _proofs.Object, _cache.Object);

    [Fact]
    public async Task Handle_KnownAndUnknownItems_FlagsOnlyFromKnown()
    {
        _inventory.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlayerInventoryItem { UserId = _userId, ItemId = "known-armor" },
                new PlayerInventoryItem { UserId = _userId, ItemId = "gone-item" },
            ]);
        _proofs.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PlayerBuildProof { UserId = _userId, BuildType = BuildType.Speed }]);
        _cache.Setup(c => c.GetById("known-armor"))
            .Returns(new ItemDto("known-armor", "armor/combined", "Скиф-5", "i.png", "RANK_MASTER"));
        _cache.Setup(c => c.GetById("gone-item")).Returns((ItemDto?)null);

        var result = await BuildHandler().Handle(
            new GetPlayerInventoryQuery(_userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!;
        dto.Items.Should().HaveCount(2);
        dto.Items[0].Name.Should().Be("Скиф-5");
        dto.Items[1].Unknown.Should().BeTrue();
        dto.Flags.Bio.Should().BeTrue();
        dto.Flags.Speed.Should().BeTrue();
        dto.Flags.Vitality.Should().BeFalse();
        dto.Flags.Combat.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_EmptyInventory_EmptyDtoAllFlagsFalse()
    {
        _inventory.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _proofs.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await BuildHandler().Handle(
            new GetPlayerInventoryQuery(_userId), CancellationToken.None);

        result.Value!.Items.Should().BeEmpty();
        result.Value.Flags.Should().Be(
            new Awake.Application.Features.Inventory.Dtos.PlayerFlagsDto(false, false, false, false, false));
    }
}
