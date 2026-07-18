using Awake.Application.Common.Models;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Boosts.Commands.SetMyBoosts;

public record SetMyBoostsCommand(
    Guid UserId,
    IReadOnlyList<BoostType> BoostTypes) : IRequest<Result<bool>>;
