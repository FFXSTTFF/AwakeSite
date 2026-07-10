using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Players.Queries.GetPlayerProfile;

public record GetPlayerProfileQuery(Guid UserId) : IRequest<Result<PlayerProfileDto>>;
