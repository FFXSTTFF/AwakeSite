using Awake.Application.Features.Items.Dtos;

namespace Awake.Application.Common.Interfaces;

public interface IItemCacheService
{
    void Load(IEnumerable<ItemDto> items);
    IEnumerable<ItemDto> Search(string q, string? categoryPrefix, string? excludeCategoryPrefix = null);
    int Count { get; }
}
