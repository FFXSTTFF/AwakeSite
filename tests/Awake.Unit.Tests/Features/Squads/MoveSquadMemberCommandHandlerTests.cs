using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Squads.Commands.MoveMember;
using Awake.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Squads;

public class MoveSquadMemberCommandHandlerTests
{
    private readonly Mock<ISquadRepository> _squads = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Guid _squadId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private MoveSquadMemberCommandHandler BuildHandler() => new(_squads.Object, _users.Object);

    private void SetupBase(int targetCount = 0, SquadMember? currentMembership = null)
    {
        _squads.Setup(r => r.GetByIdAsync(_squadId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Squad { Name = "Alpha", Number = 1 });
        _users.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new User { Username = "bob" });
        _squads.Setup(r => r.GetMembershipByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(currentMembership);
        _squads.Setup(r => r.GetMemberCountAsync(_squadId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(targetCount);
    }

    [Fact]
    public async Task Handle_FromPool_AddsWithoutRemove()
    {
        SetupBase();

        var result = await BuildHandler().Handle(
            new MoveSquadMemberCommand(_squadId, _userId), CancellationToken.None);

        result.Value.Should().BeTrue();
        _squads.Verify(r => r.MoveMemberAsync(
            null,
            It.Is<SquadMember>(m => m.SquadId == _squadId && m.UserId == _userId && !m.IsLeader),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FromOtherSquad_RemovesThenAdds()
    {
        var oldSquadId = Guid.NewGuid();
        SetupBase(currentMembership: new SquadMember { SquadId = oldSquadId, UserId = _userId });

        var result = await BuildHandler().Handle(
            new MoveSquadMemberCommand(_squadId, _userId), CancellationToken.None);

        result.Value.Should().BeTrue();
        _squads.Verify(r => r.MoveMemberAsync(
            oldSquadId,
            It.Is<SquadMember>(m => m.SquadId == _squadId && m.UserId == _userId && !m.IsLeader),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SameSquad_NoOp()
    {
        SetupBase(currentMembership: new SquadMember { SquadId = _squadId, UserId = _userId });

        var result = await BuildHandler().Handle(
            new MoveSquadMemberCommand(_squadId, _userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        _squads.Verify(r => r.MoveMemberAsync(It.IsAny<Guid?>(), It.IsAny<SquadMember>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TargetFull_Fails_WithoutRemoving()
    {
        var oldSquadId = Guid.NewGuid();
        SetupBase(targetCount: 5, currentMembership: new SquadMember { SquadId = oldSquadId, UserId = _userId });

        var result = await BuildHandler().Handle(
            new MoveSquadMemberCommand(_squadId, _userId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Отряд укомплектован (5/5).");
        _squads.Verify(r => r.MoveMemberAsync(It.IsAny<Guid?>(), It.IsAny<SquadMember>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SquadNotFound_Fails()
    {
        _squads.Setup(r => r.GetByIdAsync(_squadId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((Squad?)null);

        var result = await BuildHandler().Handle(
            new MoveSquadMemberCommand(_squadId, _userId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
