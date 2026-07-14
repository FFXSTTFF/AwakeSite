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

    public async Task<Result<bool>> Handle(
        UploadBuildProofCommand request, CancellationToken cancellationToken)
    {
        if (request.Image.Length == 0)
            return Result<bool>.Failure("Файл пуст.");
        if (request.Image.Length > MaxImageBytes)
            return Result<bool>.Failure("Файл больше 2 МБ — сожми скрин и попробуй ещё раз.");

        // Не доверяем клиентскому Content-Type — определяем формат по магическим байтам.
        var detectedContentType = DetectImageContentType(request.Image);
        if (detectedContentType is null)
            return Result<bool>.Failure("Поддерживаются только PNG, JPEG и WebP.");

        var existing = await repository.GetAsync(request.UserId, request.Type, cancellationToken);
        if (existing is not null)
        {
            existing.Image = request.Image;
            existing.ContentType = detectedContentType;
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
                ContentType = detectedContentType,
            }, cancellationToken);
        }

        return Result<bool>.Success(true);
    }

    private static string? DetectImageContentType(byte[] image)
    {
        // PNG: 89 50 4E 47
        if (image.Length >= 4
            && image[0] == 0x89 && image[1] == 0x50 && image[2] == 0x4E && image[3] == 0x47)
            return "image/png";

        // JPEG: FF D8 FF
        if (image.Length >= 3
            && image[0] == 0xFF && image[1] == 0xD8 && image[2] == 0xFF)
            return "image/jpeg";

        // WebP: 'RIFF' .... 'WEBP'
        if (image.Length >= 12
            && image[0] == 'R' && image[1] == 'I' && image[2] == 'F' && image[3] == 'F'
            && image[8] == 'W' && image[9] == 'E' && image[10] == 'B' && image[11] == 'P')
            return "image/webp";

        return null;
    }
}
