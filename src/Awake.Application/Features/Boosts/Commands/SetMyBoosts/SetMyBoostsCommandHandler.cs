using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.Entities;
using MediatR;

namespace Awake.Application.Features.Boosts.Commands.SetMyBoosts;

public class SetMyBoostsCommandHandler(
    IPlayerBoostRequestRepository boostRepository
) : IRequestHandler<SetMyBoostsCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        SetMyBoostsCommand request, CancellationToken cancellationToken)
    {
        var types = request.BoostTypes.Distinct().ToList();
        await boostRepository.ReplaceForUserAsync(
            request.UserId,
            types.Select(t => new PlayerBoostRequest { UserId = request.UserId, BoostType = t }).ToList(),
            cancellationToken);
        return Result<bool>.Success(true);
    }
}
