using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Enums;

namespace Awake.Application.Common.Interfaces;

public interface IItemCacheService
{
    void Load(IEnumerable<ItemDto> items);
    IEnumerable<ItemDto> Search(string q, string? categoryPrefix, string? excludeCategoryPrefix = null, int limit = 15);
    IEnumerable<ItemDto> SearchBoosts(string q, BoostType boostType, int limit = 40);
    ItemDto? GetById(string id);
    int Count { get; }
}
