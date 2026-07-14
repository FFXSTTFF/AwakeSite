using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.Entities;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.AddInventoryItem;

public class AddInventoryItemCommandHandler(
    IPlayerInventoryRepository repository,
    IItemCacheService itemCache
) : IRequestHandler<AddInventoryItemCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        AddInventoryItemCommand request, CancellationToken cancellationToken)
    {
        if (itemCache.GetById(request.ItemId) is null)
            return Result<bool>.Failure("Предмет не найден в базе.");

        if (await repository.GetAsync(request.UserId, request.ItemId, cancellationToken) is not null)
            return Result<bool>.Failure("Этот предмет уже в инвентаре.");

        await repository.AddAsync(new PlayerInventoryItem
        {
            UserId = request.UserId,
            ItemId = request.ItemId,
        }, cancellationToken);

        return Result<bool>.Success(true);
    }
}
