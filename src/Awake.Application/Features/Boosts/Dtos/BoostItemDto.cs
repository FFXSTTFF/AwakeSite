using Awake.Domain.Enums;

namespace Awake.Application.Features.Boosts.Dtos;

public record BoostItemDto(BoostType BoostType, string ItemId, string Name, string? Icon);

public record BoostSelectionDto(BoostType BoostType, string ItemId);
