using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Tickets.Commands.AddTicketComment;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using Moq;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.Tickets;

public class AddTicketCommentCommandHandlerTests
{
    private readonly Mock<ITicketRepository> _repo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    [Fact]
    public async Task Handle_ValidCommand_AddsComment()
    {
        var userId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var user = new User { Id = userId, Username = "officer1" };
        var ticket = new Ticket { Id = ticketId, Status = TicketStatus.InReview };

        _repo.Setup(r => r.GetByIdAsync(ticketId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(ticket);
        _userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);

        TicketComment? saved = null;
        _repo.Setup(r => r.AddCommentAsync(It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()))
             .Callback<TicketComment, CancellationToken>((c, _) => saved = c)
             .Returns(Task.CompletedTask);

        _currentUser.Setup(s => s.UserId).Returns(userId);

        var handler = new AddTicketCommentCommandHandler(_repo.Object, _userRepo.Object, _currentUser.Object);
        var result = await handler.Handle(
            new AddTicketCommentCommand(ticketId, "Looks good."), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Should().Be("Looks good.");
        result.Value.AuthorUsername.Should().Be("officer1");
        saved!.TicketId.Should().Be(ticketId);
        saved.AuthorId.Should().Be(userId);
    }

    [Fact]
    public async Task Handle_TicketNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Ticket?)null);
        _currentUser.Setup(s => s.UserId).Returns(Guid.NewGuid());

        var handler = new AddTicketCommentCommandHandler(_repo.Object, _userRepo.Object, _currentUser.Object);
        var result = await handler.Handle(
            new AddTicketCommentCommand(Guid.NewGuid(), "A comment"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
