using Awake.Application.Common.Models;
using Awake.Application.Features.Boosts.Dtos;
using MediatR;

namespace Awake.Application.Features.Boosts.Commands.SetMyBoosts;

public record SetMyBoostsCommand(
    Guid UserId,
    IReadOnlyList<BoostSelectionDto> Selections) : IRequest<Result<bool>>;
