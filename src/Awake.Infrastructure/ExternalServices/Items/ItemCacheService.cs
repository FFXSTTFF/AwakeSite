using Awake.Application.Common.Interfaces;
using Awake.Application.Features.Items;
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Enums;

namespace Awake.Infrastructure.ExternalServices.Items;

public class ItemCacheService : IItemCacheService
{
    private Dictionary<string, ItemDto> _items = [];

    public int Count => _items.Count;

    public void Load(IEnumerable<ItemDto> items) =>
        _items = items.ToDictionary(x => x.Id);

    public IEnumerable<ItemDto> Search(string q, string? categoryPrefix, string? excludeCategoryPrefix = null, int limit = 15) =>
        _items.Values
            .Where(x => ItemRanks.VeteranPlus.Contains(x.Color))
            .Where(x => categoryPrefix == null || x.Category.StartsWith(categoryPrefix))
            .Where(x => excludeCategoryPrefix == null || !x.Category.StartsWith(excludeCategoryPrefix))
            .Where(x => string.IsNullOrEmpty(q) || x.NameRu.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Take(limit);

    public IEnumerable<ItemDto> SearchBoosts(string q, BoostType boostType, int limit = 40) =>
        _items.Values
            .Where(x => x.BoostType == boostType)
            .Where(x => ItemRanks.VeteranPlus.Contains(x.Color))
            .Where(x => string.IsNullOrEmpty(q) || x.NameRu.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.NameRu, StringComparer.OrdinalIgnoreCase)
            .Take(limit);

    public ItemDto? GetById(string id) =>
        _items.TryGetValue(id, out var item) ? item : null;
}
