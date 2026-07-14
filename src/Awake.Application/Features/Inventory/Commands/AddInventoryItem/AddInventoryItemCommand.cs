using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.AddInventoryItem;

public record AddInventoryItemCommand(Guid UserId, string ItemId) : IRequest<Result<bool>>;
