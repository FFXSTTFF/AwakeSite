using Awake.Application.Common.Models;
using Awake.Application.Features.Inventory.Dtos;
using MediatR;

namespace Awake.Application.Features.Inventory.Queries.GetPlayerInventory;

public record GetPlayerInventoryQuery(Guid UserId) : IRequest<Result<PlayerInventoryDto>>;
