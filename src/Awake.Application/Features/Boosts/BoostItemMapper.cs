using Awake.Application.Common.Interfaces;
using Awake.Application.Features.Boosts.Dtos;
using Awake.Domain.Entities;

namespace Awake.Application.Features.Boosts;

public static class BoostItemMapper
{
    /// <summary>Предмет мог исчезнуть из кэша после патча игры — тогда name = itemId, без иконки.</summary>
    public static BoostItemDto ToDto(PlayerBoostRequest request, IItemCacheService itemCache)
    {
        var item = itemCache.GetById(request.ItemId);
        return new BoostItemDto(request.BoostType, request.ItemId, item?.NameRu ?? request.ItemId, item?.Icon);
    }
}
