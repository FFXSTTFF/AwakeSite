using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Application.Features.Inventory.Dtos;
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Inventory.Queries.GetPlayerInventory;

public class GetPlayerInventoryQueryHandler(
    IPlayerInventoryRepository inventoryRepository,
    IPlayerBuildProofRepository proofRepository,
    IItemCacheService itemCache
) : IRequestHandler<GetPlayerInventoryQuery, Result<PlayerInventoryDto>>
{
    public async Task<Result<PlayerInventoryDto>> Handle(
        GetPlayerInventoryQuery request, CancellationToken cancellationToken)
    {
        var entries = await inventoryRepository.GetByUserAsync(request.UserId, cancellationToken);
        var proofs = await proofRepository.GetByUserAsync(request.UserId, cancellationToken);

        var known = new List<ItemDto>();
        var items = new List<InventoryItemDto>();
        foreach (var entry in entries)
        {
            var item = itemCache.GetById(entry.ItemId);
            if (item is null)
            {
                items.Add(new InventoryItemDto(entry.ItemId, "Неизвестный предмет",
                    null, null, null, Unknown: true));
            }
            else
            {
                known.Add(item);
                items.Add(new InventoryItemDto(item.Id, item.NameRu,
                    item.Icon, item.Color, item.Category, Unknown: false));
            }
        }

        var flags = PlayerFlagsCalculator.Calculate(
            known,
            hasSpeedProof: proofs.Any(p => p.BuildType == BuildType.Speed),
            hasVitalityProof: proofs.Any(p => p.BuildType == BuildType.Vitality));

        return Result<PlayerInventoryDto>.Success(new PlayerInventoryDto(items, flags));
    }
}
