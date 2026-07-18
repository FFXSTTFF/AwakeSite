using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Squads.Commands.RenameSquad;
using Awake.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Squads;

public class RenameSquadCommandHandlerTests
{
    private readonly Mock<ISquadRepository> _squads = new();
    private readonly Guid _squadId = Guid.NewGuid();

    private RenameSquadCommandHandler BuildHandler() => new(_squads.Object);

    [Fact]
    public async Task Handle_ValidName_TrimsAndPersists()
    {
        var squad = new Squad { Name = "Отряд 1", Number = 1 };
        _squads.Setup(r => r.GetByIdAsync(_squadId, It.IsAny<CancellationToken>())).ReturnsAsync(squad);

        var result = await BuildHandler().Handle(
            new RenameSquadCommand(_squadId, "  Ночная смена  "), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        squad.Name.Should().Be("Ночная смена");
        _squads.Verify(r => r.UpdateAsync(squad, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SquadNotFound_Fails()
    {
        _squads.Setup(r => r.GetByIdAsync(_squadId, It.IsAny<CancellationToken>())).ReturnsAsync((Squad?)null);

        var result = await BuildHandler().Handle(
            new RenameSquadCommand(_squadId, "Ночная смена"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _squads.Verify(r => r.UpdateAsync(It.IsAny<Squad>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
