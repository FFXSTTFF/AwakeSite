using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Inventory.Commands.AddInventoryItem;
using Awake.Application.Features.Inventory.Commands.DeleteBuildProof;
using Awake.Application.Features.Inventory.Commands.UploadBuildProof;
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Inventory;

public class InventoryCommandsTests
{
    private readonly Mock<IPlayerInventoryRepository> _inventory = new();
    private readonly Mock<IPlayerBuildProofRepository> _proofs = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task AddItem_UnknownInCache_Fails()
    {
        _cache.Setup(c => c.GetById("nope")).Returns((ItemDto?)null);
        var handler = new AddInventoryItemCommandHandler(_inventory.Object, _cache.Object);

        var result = await handler.Handle(
            new AddInventoryItemCommand(_userId, "nope"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _inventory.Verify(r => r.AddAsync(It.IsAny<PlayerInventoryItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddItem_Duplicate_Fails()
    {
        _cache.Setup(c => c.GetById("armor1"))
              .Returns(new ItemDto("armor1", "armor/combat", "Броня", "i.png", ""));
        _inventory.Setup(r => r.GetAsync(_userId, "armor1", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PlayerInventoryItem { UserId = _userId, ItemId = "armor1" });
        var handler = new AddInventoryItemCommandHandler(_inventory.Object, _cache.Object);

        var result = await handler.Handle(
            new AddInventoryItemCommand(_userId, "armor1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task AddItem_Valid_Saves()
    {
        _cache.Setup(c => c.GetById("armor1"))
              .Returns(new ItemDto("armor1", "armor/combat", "Броня", "i.png", ""));
        _inventory.Setup(r => r.GetAsync(_userId, "armor1", It.IsAny<CancellationToken>()))
                  .ReturnsAsync((PlayerInventoryItem?)null);
        var handler = new AddInventoryItemCommandHandler(_inventory.Object, _cache.Object);

        var result = await handler.Handle(
            new AddInventoryItemCommand(_userId, "armor1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _inventory.Verify(r => r.AddAsync(
            It.Is<PlayerInventoryItem>(i => i.UserId == _userId && i.ItemId == "armor1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("application/pdf")]
    [InlineData("text/html")]
    public async Task UploadProof_BadContentType_Fails(string contentType)
    {
        var handler = new UploadBuildProofCommandHandler(_proofs.Object);

        var result = await handler.Handle(new UploadBuildProofCommand(
            _userId, BuildType.Speed, [1, 2, 3], contentType), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UploadProof_TooLarge_Fails()
    {
        var handler = new UploadBuildProofCommandHandler(_proofs.Object);
        var big = new byte[UploadBuildProofCommandHandler.MaxImageBytes + 1];

        var result = await handler.Handle(new UploadBuildProofCommand(
            _userId, BuildType.Speed, big, "image/png"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UploadProof_New_Adds()
    {
        _proofs.Setup(r => r.GetAsync(_userId, BuildType.Speed, It.IsAny<CancellationToken>()))
               .ReturnsAsync((PlayerBuildProof?)null);
        var handler = new UploadBuildProofCommandHandler(_proofs.Object);

        var result = await handler.Handle(new UploadBuildProofCommand(
            _userId, BuildType.Speed, [1, 2, 3], "image/png"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _proofs.Verify(r => r.AddAsync(
            It.Is<PlayerBuildProof>(p => p.UserId == _userId && p.BuildType == BuildType.Speed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadProof_Existing_Replaces()
    {
        var existing = new PlayerBuildProof
        {
            UserId = _userId, BuildType = BuildType.Vitality,
            Image = [9], ContentType = "image/png",
        };
        _proofs.Setup(r => r.GetAsync(_userId, BuildType.Vitality, It.IsAny<CancellationToken>()))
               .ReturnsAsync(existing);
        var handler = new UploadBuildProofCommandHandler(_proofs.Object);

        var result = await handler.Handle(new UploadBuildProofCommand(
            _userId, BuildType.Vitality, [1, 2], "image/webp"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        existing.Image.Should().Equal(1, 2);
        existing.ContentType.Should().Be("image/webp");
        _proofs.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _proofs.Verify(r => r.AddAsync(It.IsAny<PlayerBuildProof>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteProof_Missing_Fails()
    {
        _proofs.Setup(r => r.GetAsync(_userId, BuildType.Speed, It.IsAny<CancellationToken>()))
               .ReturnsAsync((PlayerBuildProof?)null);
        var handler = new DeleteBuildProofCommandHandler(_proofs.Object);

        var result = await handler.Handle(
            new DeleteBuildProofCommand(_userId, BuildType.Speed), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
