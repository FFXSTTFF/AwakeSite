using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.UpdateMyLoadout;

/// <summary>Слот в запросе: только id предмета и заточка; имя и иконку резолвит сервер.</summary>
public record LoadoutSlotRequest(string ItemId, int Upgrade);

public record UpdateMyLoadoutCommand(
    Guid UserId,
    LoadoutSlotRequest? Sniper,
    LoadoutSlotRequest? Weapon,
    LoadoutSlotRequest? Armor
) : IRequest<Result<bool>>;
