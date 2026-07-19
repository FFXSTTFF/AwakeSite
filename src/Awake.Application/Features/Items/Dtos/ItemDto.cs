using Awake.Domain.Enums;

namespace Awake.Application.Features.Items.Dtos;

public record ItemDto(string Id, string Category, string NameRu, string Icon, string Color, BoostType? BoostType = null);
