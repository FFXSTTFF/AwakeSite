using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Tickets.Commands.UpdateTicketStatus;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using Moq;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.Tickets;

public class UpdateTicketStatusCommandHandlerTests
{
    private readonly Mock<ITicketRepository> _repo = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IDiscordNotifier> _discord = new();

    [Fact]
    public async Task Handle_ApproveTicket_SetsReviewedAndNotifies()
    {
        var reviewerId = Guid.NewGuid();
        var ticket = new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.InReview, GameNickname = "Bob" };

        _repo.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(ticket);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);
        _currentUser.Setup(s => s.UserId).Returns(reviewerId);
        _discord.Setup(d => d.NotifyTicketDecisionAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var handler = new UpdateTicketStatusCommandHandler(_repo.Object, _currentUser.Object, _discord.Object);
        var result = await handler.Handle(
            new UpdateTicketStatusCommand(ticket.Id, TicketStatus.Approved), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketStatus.Approved);
        ticket.ReviewedBy.Should().Be(reviewerId);
        ticket.ReviewedAt.Should().NotBeNull();
        _discord.Verify(d => d.NotifyTicketDecisionAsync(ticket, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SetInReview_DoesNotNotify()
    {
        var ticket = new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Pending, GameNickname = "Carol" };
        _repo.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(ticket);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);
        _currentUser.Setup(s => s.UserId).Returns(Guid.NewGuid());

        var handler = new UpdateTicketStatusCommandHandler(_repo.Object, _currentUser.Object, _discord.Object);
        var result = await handler.Handle(
            new UpdateTicketStatusCommand(ticket.Id, TicketStatus.InReview), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _discord.Verify(d => d.NotifyTicketDecisionAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TicketNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Ticket?)null);
        _currentUser.Setup(s => s.UserId).Returns(Guid.NewGuid());

        var handler = new UpdateTicketStatusCommandHandler(_repo.Object, _currentUser.Object, _discord.Object);
        var result = await handler.Handle(
            new UpdateTicketStatusCommand(Guid.NewGuid(), TicketStatus.InReview), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
