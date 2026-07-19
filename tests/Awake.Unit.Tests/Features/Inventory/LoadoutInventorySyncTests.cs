using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Inventory;
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Entities;
using Awake.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Inventory;

public class LoadoutInventorySyncTests
{
    private readonly Mock<IPlayerInventoryRepository> _inventory = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private readonly Guid _userId = Guid.NewGuid();

    private void SetupCached(string itemId) =>
        _cache.Setup(c => c.GetById(itemId))
              .Returns(new ItemDto(itemId, "weapon/assault_rifle", "Предмет", "i.png", ""));

    private static Loadout MakeLoadout(string? sniperId, string weaponId, string armorId) =>
        new(
            sniperId is null ? null : new LoadoutSlot(sniperId, "Снайперка", "i.png", 0),
            new LoadoutSlot(weaponId, "Оружие", "i.png", 0),
            new LoadoutSlot(armorId, "Броня", "i.png", 0));

    [Fact]
    public async Task AddItems_AllSlotsNew_AddsAll()
    {
        SetupCached("s1");
        SetupCached("w1");
        SetupCached("a1");
        _inventory.Setup(r => r.GetAsync(_userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((PlayerInventoryItem?)null);

        await LoadoutInventorySync.AddItemsAsync(
            _inventory.Object, _cache.Object, _userId, MakeLoadout("s1", "w1", "a1"));

        _inventory.Verify(r => r.AddAsync(
            It.Is<PlayerInventoryItem>(i => i.UserId == _userId && i.ItemId == "s1"),
            It.IsAny<CancellationToken>()), Times.Once);
        _inventory.Verify(r => r.AddAsync(
            It.Is<PlayerInventoryItem>(i => i.UserId == _userId && i.ItemId == "w1"),
            It.IsAny<CancellationToken>()), Times.Once);
        _inventory.Verify(r => r.AddAsync(
            It.Is<PlayerInventoryItem>(i => i.UserId == _userId && i.ItemId == "a1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddItems_AlreadyInInventory_Skips()
    {
        SetupCached("w1");
        SetupCached("a1");
        _inventory.Setup(r => r.GetAsync(_userId, "w1", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PlayerInventoryItem { UserId = _userId, ItemId = "w1" });
        _inventory.Setup(r => r.GetAsync(_userId, "a1", It.IsAny<CancellationToken>()))
                  .ReturnsAsync((PlayerInventoryItem?)null);

        await LoadoutInventorySync.AddItemsAsync(
            _inventory.Object, _cache.Object, _userId, MakeLoadout(null, "w1", "a1"));

        _inventory.Verify(r => r.AddAsync(
            It.Is<PlayerInventoryItem>(i => i.ItemId == "w1"), It.IsAny<CancellationToken>()), Times.Never);
        _inventory.Verify(r => r.AddAsync(
            It.Is<PlayerInventoryItem>(i => i.ItemId == "a1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddItems_NoSniper_AddsOnlyWeaponAndArmor()
    {
        SetupCached("w1");
        SetupCached("a1");
        _inventory.Setup(r => r.GetAsync(_userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((PlayerInventoryItem?)null);

        await LoadoutInventorySync.AddItemsAsync(
            _inventory.Object, _cache.Object, _userId, MakeLoadout(null, "w1", "a1"));

        _inventory.Verify(r => r.AddAsync(
            It.IsAny<PlayerInventoryItem>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task AddItems_ItemMissingFromCache_Skips()
    {
        _cache.Setup(c => c.GetById("ghost")).Returns((ItemDto?)null);
        SetupCached("a1");
        _inventory.Setup(r => r.GetAsync(_userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((PlayerInventoryItem?)null);

        await LoadoutInventorySync.AddItemsAsync(
            _inventory.Object, _cache.Object, _userId, MakeLoadout(null, "ghost", "a1"));

        _inventory.Verify(r => r.AddAsync(
            It.Is<PlayerInventoryItem>(i => i.ItemId == "ghost"), It.IsAny<CancellationToken>()), Times.Never);
        _inventory.Verify(r => r.AddAsync(
            It.Is<PlayerInventoryItem>(i => i.ItemId == "a1"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
