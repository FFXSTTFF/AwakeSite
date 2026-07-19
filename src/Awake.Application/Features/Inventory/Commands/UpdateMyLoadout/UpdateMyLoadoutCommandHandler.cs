using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.ValueObjects;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.UpdateMyLoadout;

public class UpdateMyLoadoutCommandHandler(
    IUserRepository users,
    IPlayerInventoryRepository inventory,
    IItemCacheService itemCache
) : IRequestHandler<UpdateMyLoadoutCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        UpdateMyLoadoutCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result<bool>.Failure("Пользователь не найден.");

        if (request.Weapon is null || request.Armor is null)
            return Result<bool>.Failure("Укажи основное оружие и броню.");

        var (weapon, weaponError) = await BuildSlotAsync(request.UserId, request.Weapon, cancellationToken);
        if (weaponError is not null)
            return Result<bool>.Failure(weaponError);

        var (armor, armorError) = await BuildSlotAsync(request.UserId, request.Armor, cancellationToken);
        if (armorError is not null)
            return Result<bool>.Failure(armorError);

        LoadoutSlot? sniper = null;
        if (request.Sniper is not null)
        {
            (sniper, var sniperError) = await BuildSlotAsync(request.UserId, request.Sniper, cancellationToken);
            if (sniperError is not null)
                return Result<bool>.Failure(sniperError);
        }

        user.Loadout = new Loadout(sniper, weapon!, armor!);
        await users.UpdateAsync(user, cancellationToken);

        return Result<bool>.Success(true);
    }

    private async Task<(LoadoutSlot? Slot, string? Error)> BuildSlotAsync(
        Guid userId, LoadoutSlotRequest slot, CancellationToken ct)
    {
        if (slot.Upgrade is < 0 or > 15)
            return (null, "Заточка должна быть от 0 до 15.");

        if (await inventory.GetAsync(userId, slot.ItemId, ct) is null)
            return (null, "Предмет не найден в твоём инвентаре.");

        var item = itemCache.GetById(slot.ItemId);
        if (item is null)
            return (null, "Предмет не найден в базе.");

        return (new LoadoutSlot(slot.ItemId, item.NameRu, item.Icon, slot.Upgrade), null);
    }
}
