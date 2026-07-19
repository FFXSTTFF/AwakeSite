using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Application.Features.Items;
using Awake.Domain.Entities;
using MediatR;

namespace Awake.Application.Features.Boosts.Commands.SetMyBoosts;

public class SetMyBoostsCommandHandler(
    IPlayerBoostRequestRepository boostRepository,
    IItemCacheService itemCache
) : IRequestHandler<SetMyBoostsCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        SetMyBoostsCommand request, CancellationToken cancellationToken)
    {
        // Защита от кривых запросов в обход UI: предмет существует,
        // тип совпадает (null у не-бустов не совпадёт), ранг Ветеран+.
        foreach (var sel in request.Selections)
        {
            var item = itemCache.GetById(sel.ItemId);
            if (item is null)
                return Result<bool>.Failure($"Предмет не найден: {sel.ItemId}");
            if (item.BoostType != sel.BoostType)
                return Result<bool>.Failure($"Предмет не подходит для этого слота: {item.NameRu}");
            if (!ItemRanks.VeteranPlus.Contains(item.Color))
                return Result<bool>.Failure($"Ранг предмета ниже Ветерана: {item.NameRu}");
        }

        var requests = request.Selections
            .Select(s => new PlayerBoostRequest
            {
                UserId = request.UserId,
                BoostType = s.BoostType,
                ItemId = s.ItemId,
            })
            .ToList();
        await boostRepository.ReplaceForUserAsync(request.UserId, requests, cancellationToken);
        return Result<bool>.Success(true);
    }
}
