using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.DeleteBuildProof;

public class DeleteBuildProofCommandHandler(
    IPlayerBuildProofRepository repository
) : IRequestHandler<DeleteBuildProofCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        DeleteBuildProofCommand request, CancellationToken cancellationToken)
    {
        var proof = await repository.GetAsync(request.UserId, request.Type, cancellationToken);
        if (proof is null)
            return Result<bool>.Failure("Пруф не найден.");

        await repository.RemoveAsync(proof, cancellationToken);
        return Result<bool>.Success(true);
    }
}
