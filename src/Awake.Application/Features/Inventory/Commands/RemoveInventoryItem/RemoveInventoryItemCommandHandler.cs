using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.RemoveInventoryItem;

public class RemoveInventoryItemCommandHandler(
    IPlayerInventoryRepository repository
) : IRequestHandler<RemoveInventoryItemCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        RemoveInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var entry = await repository.GetAsync(request.UserId, request.ItemId, cancellationToken);
        if (entry is null)
            return Result<bool>.Failure("Предмета нет в инвентаре.");

        await repository.RemoveAsync(entry, cancellationToken);
        return Result<bool>.Success(true);
    }
}
