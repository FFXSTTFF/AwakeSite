namespace Awake.Application.Features.Inventory.Dtos;

public record PlayerFlagsDto(bool Bio, bool Combat, bool Sniper, bool Speed, bool Vitality);

/// <summary>Unknown = предмет пропал из базы stalzone после пересинка; во флагах не участвует.</summary>
public record InventoryItemDto(
    string ItemId, string Name, string? Icon, string? Color, string? Category, bool Unknown);

public record PlayerInventoryDto(
    IReadOnlyList<InventoryItemDto> Items, PlayerFlagsDto Flags);
