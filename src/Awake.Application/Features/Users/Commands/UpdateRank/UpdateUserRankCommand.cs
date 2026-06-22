using Awake.Application.Common.Models;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Users.Commands.UpdateRank;

public record UpdateUserRankCommand(Guid UserId, UserRank NewRank) : IRequest<Result<Unit>>;
