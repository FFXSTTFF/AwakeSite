using Awake.API.Filters;
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Inventory.Commands.AddInventoryItem;
using Awake.Application.Features.Inventory.Commands.DeleteBuildProof;
using Awake.Application.Features.Inventory.Commands.RemoveInventoryItem;
using Awake.Application.Features.Inventory.Commands.UploadBuildProof;
using Awake.Application.Features.Inventory.Queries.GetPlayerInventory;
using Awake.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Awake.API.Controllers;

public record AddItemRequest(string ItemId);

[ApiController]
[Authorize]
public class InventoryController(
    ISender sender,
    ICurrentUserService currentUser,
    IPlayerBuildProofRepository proofRepository
) : ControllerBase
{
    // ── Свой инвентарь (любой ранг) ─────────────────────────────────────────

    [HttpGet("api/profile/inventory")]
    public async Task<IActionResult> GetMyInventory(CancellationToken ct)
    {
        var result = await sender.Send(new GetPlayerInventoryQuery(currentUser.UserId), ct);
        return Ok(result.Value);
    }

    [HttpPost("api/profile/inventory/items")]
    public async Task<IActionResult> AddItem(AddItemRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new AddInventoryItemCommand(currentUser.UserId, request.ItemId), ct);
        return result.IsSuccess
            ? Ok()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    [HttpDelete("api/profile/inventory/items/{itemId}")]
    public async Task<IActionResult> RemoveItem(string itemId, CancellationToken ct)
    {
        var result = await sender.Send(
            new RemoveInventoryItemCommand(currentUser.UserId, itemId), ct);
        return result.IsSuccess
            ? Ok()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    [HttpPost("api/profile/build-proof")]
    [RequestSizeLimit(4_194_304)] // запас над лимитом 2 МБ, чтобы отдавать своё сообщение об ошибке
    public async Task<IActionResult> UploadProof(
        [FromForm] BuildType type, IFormFile file, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        var result = await sender.Send(new UploadBuildProofCommand(
            currentUser.UserId, type, ms.ToArray(), file.ContentType), ct);
        return result.IsSuccess
            ? Ok()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    [HttpDelete("api/profile/build-proof/{type}")]
    public async Task<IActionResult> DeleteMyProof(BuildType type, CancellationToken ct)
    {
        var result = await sender.Send(
            new DeleteBuildProofCommand(currentUser.UserId, type), ct);
        return result.IsSuccess
            ? Ok()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status404NotFound);
    }

    // ── Чужой инвентарь ─────────────────────────────────────────────────────

    [HttpGet("api/players/{userId:guid}/inventory")]
    [RankAuthorize(UserRank.Member)]
    public async Task<IActionResult> GetInventory(Guid userId, CancellationToken ct)
    {
        var result = await sender.Send(new GetPlayerInventoryQuery(userId), ct);
        return Ok(result.Value);
    }

    [HttpGet("api/players/{userId:guid}/build-proof/{type}/image")]
    public async Task<IActionResult> GetProofImage(Guid userId, BuildType type, CancellationToken ct)
    {
        if (!IsOwnerOrOfficer(userId))
            return Forbid();

        var proof = await proofRepository.GetAsync(userId, type, ct);
        if (proof is null)
            return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(proof.Image, proof.ContentType);
    }

    [HttpDelete("api/players/{userId:guid}/build-proof/{type}")]
    public async Task<IActionResult> DeleteProof(Guid userId, BuildType type, CancellationToken ct)
    {
        if (!IsOwnerOrOfficer(userId))
            return Forbid();

        var result = await sender.Send(new DeleteBuildProofCommand(userId, type), ct);
        return result.IsSuccess
            ? Ok()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status404NotFound);
    }

    // Пруф-скрины: смотреть/удалять может сам владелец и Officer+
    private bool IsOwnerOrOfficer(Guid userId) =>
        currentUser.UserId == userId || currentUser.Rank >= UserRank.Officer;
}
