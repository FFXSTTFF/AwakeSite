using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Inventory.Commands.UpdateMyLoadout;
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Inventory;

public class UpdateMyLoadoutCommandHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPlayerInventoryRepository> _inventory = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly User _user;

    public UpdateMyLoadoutCommandHandlerTests()
    {
        _user = new User { Id = _userId, Username = "test" };
        _users.Setup(u => u.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(_user);
    }

    private UpdateMyLoadoutCommandHandler BuildHandler() =>
        new(_users.Object, _inventory.Object, _cache.Object);

    private void SetupItem(string itemId, string category, string name = "Предмет")
    {
        _inventory.Setup(r => r.GetAsync(_userId, itemId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PlayerInventoryItem { UserId = _userId, ItemId = itemId });
        _cache.Setup(c => c.GetById(itemId))
              .Returns(new ItemDto(itemId, category, name, "icon.png", ""));
    }

    [Fact]
    public async Task Handle_UserNotFound_Fails()
    {
        _users.Setup(u => u.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
              .ReturnsAsync((User?)null);

        var result = await BuildHandler().Handle(new UpdateMyLoadoutCommand(
            _userId, null, new LoadoutSlotRequest("w1", 0), new LoadoutSlotRequest("a1", 0)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WeaponMissing_Fails()
    {
        SetupItem("a1", "armor/combat");

        var result = await BuildHandler().Handle(new UpdateMyLoadoutCommand(
            _userId, null, null, new LoadoutSlotRequest("a1", 0)), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ArmorMissing_Fails()
    {
        SetupItem("w1", "weapon/assault_rifle");

        var result = await BuildHandler().Handle(new UpdateMyLoadoutCommand(
            _userId, null, new LoadoutSlotRequest("w1", 0), null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    public async Task Handle_UpgradeOutOfRange_Fails(int upgrade)
    {
        SetupItem("w1", "weapon/assault_rifle");
        SetupItem("a1", "armor/combat");

        var result = await BuildHandler().Handle(new UpdateMyLoadoutCommand(
            _userId, null, new LoadoutSlotRequest("w1", upgrade), new LoadoutSlotRequest("a1", 0)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ItemNotInInventory_Fails()
    {
        SetupItem("a1", "armor/combat");
        _inventory.Setup(r => r.GetAsync(_userId, "w1", It.IsAny<CancellationToken>()))
                  .ReturnsAsync((PlayerInventoryItem?)null);

        var result = await BuildHandler().Handle(new UpdateMyLoadoutCommand(
            _userId, null, new LoadoutSlotRequest("w1", 0), new LoadoutSlotRequest("a1", 0)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ItemGoneFromItemBase_Fails()
    {
        _inventory.Setup(r => r.GetAsync(_userId, "w1", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PlayerInventoryItem { UserId = _userId, ItemId = "w1" });
        _cache.Setup(c => c.GetById("w1")).Returns((ItemDto?)null);
        SetupItem("a1", "armor/combat");

        var result = await BuildHandler().Handle(new UpdateMyLoadoutCommand(
            _userId, null, new LoadoutSlotRequest("w1", 0), new LoadoutSlotRequest("a1", 0)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Valid_SavesLoadoutWithResolvedNames()
    {
        SetupItem("w1", "weapon/assault_rifle", "АК-74М");
        SetupItem("a1", "armor/combat", "Заря");

        var result = await BuildHandler().Handle(new UpdateMyLoadoutCommand(
            _userId, null, new LoadoutSlotRequest("w1", 12), new LoadoutSlotRequest("a1", 5)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _user.Loadout.Should().NotBeNull();
        _user.Loadout!.Sniper.Should().BeNull();
        _user.Loadout.Weapon.ItemId.Should().Be("w1");
        _user.Loadout.Weapon.ItemName.Should().Be("АК-74М");
        _user.Loadout.Weapon.Upgrade.Should().Be(12);
        _user.Loadout.Armor.ItemName.Should().Be("Заря");
        _users.Verify(u => u.UpdateAsync(_user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithSniper_SavesSniperSlot()
    {
        SetupItem("s1", "weapon/sniper_rifle", "СВД");
        SetupItem("w1", "weapon/assault_rifle");
        SetupItem("a1", "armor/combat");

        var result = await BuildHandler().Handle(new UpdateMyLoadoutCommand(
            _userId, new LoadoutSlotRequest("s1", 3),
            new LoadoutSlotRequest("w1", 0), new LoadoutSlotRequest("a1", 0)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _user.Loadout!.Sniper!.ItemName.Should().Be("СВД");
        _user.Loadout.Sniper.Upgrade.Should().Be(3);
    }
}
