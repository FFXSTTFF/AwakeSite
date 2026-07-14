using Awake.Application.Common.Models;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.DeleteBuildProof;

public record DeleteBuildProofCommand(Guid UserId, BuildType Type) : IRequest<Result<bool>>;
