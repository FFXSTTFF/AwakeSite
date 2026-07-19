using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Entities;
using Awake.Domain.ValueObjects;

namespace Awake.Application.Features.Inventory;

/// <summary>
/// Добавляет предметы экипировки из заявки в инвентарь игрока.
/// Дубликаты и предметы, отсутствующие в базе stalzone, молча пропускаются.
/// </summary>
public static class LoadoutInventorySync
{
    public static async Task AddItemsAsync(
        IPlayerInventoryRepository inventory,
        IItemCacheService itemCache,
        Guid userId,
        Loadout loadout,
        CancellationToken ct = default)
    {
        foreach (var slot in new[] { loadout.Sniper, loadout.Weapon, loadout.Armor })
        {
            if (slot is null || itemCache.GetById(slot.ItemId) is null)
                continue;

            if (await inventory.GetAsync(userId, slot.ItemId, ct) is not null)
                continue;

            await inventory.AddAsync(new PlayerInventoryItem
            {
                UserId = userId,
                ItemId = slot.ItemId,
            }, ct);
        }
    }
}
