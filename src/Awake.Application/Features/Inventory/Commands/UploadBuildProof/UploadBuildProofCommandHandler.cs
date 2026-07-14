using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.Entities;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.UploadBuildProof;

public class UploadBuildProofCommandHandler(
    IPlayerBuildProofRepository repository
) : IRequestHandler<UploadBuildProofCommand, Result<bool>>
{
    public const int MaxImageBytes = 2_097_152; // 2 МБ
    private static readonly string[] AllowedContentTypes =
        ["image/png", "image/jpeg", "image/webp"];

    public async Task<Result<bool>> Handle(
        UploadBuildProofCommand request, CancellationToken cancellationToken)
    {
        if (request.Image.Length == 0)
            return Result<bool>.Failure("Файл пуст.");
        if (request.Image.Length > MaxImageBytes)
            return Result<bool>.Failure("Файл больше 2 МБ — сожми скрин и попробуй ещё раз.");
        if (!AllowedContentTypes.Contains(request.ContentType))
            return Result<bool>.Failure("Поддерживаются только PNG, JPEG и WebP.");

        var existing = await repository.GetAsync(request.UserId, request.Type, cancellationToken);
        if (existing is not null)
        {
            existing.Image = request.Image;
            existing.ContentType = request.ContentType;
            existing.UpdatedAt = DateTime.UtcNow;
            await repository.UpdateAsync(existing, cancellationToken);
        }
        else
        {
            await repository.AddAsync(new PlayerBuildProof
            {
                UserId = request.UserId,
                BuildType = request.Type,
                Image = request.Image,
                ContentType = request.ContentType,
            }, cancellationToken);
        }

        return Result<bool>.Success(true);
    }
}
