using Awake.Application.Common.Models;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.UploadBuildProof;

public record UploadBuildProofCommand(
    Guid UserId, BuildType Type, byte[] Image, string ContentType) : IRequest<Result<bool>>;
