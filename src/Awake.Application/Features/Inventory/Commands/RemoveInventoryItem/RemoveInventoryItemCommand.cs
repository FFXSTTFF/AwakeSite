using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.RemoveInventoryItem;

public record RemoveInventoryItemCommand(Guid UserId, string ItemId) : IRequest<Result<bool>>;
