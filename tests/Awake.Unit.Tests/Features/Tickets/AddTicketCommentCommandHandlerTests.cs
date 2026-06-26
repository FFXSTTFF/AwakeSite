using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Tickets.Commands.AddTicketComment;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Tickets;

public class AddTicketCommentCommandHandlerTests
{
    private readonly Mock<ITicketRepository> _repo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<IDiscordBotService> _discord = new();

    private AddTicketCommentCommandHandler BuildHandler() =>
        new(_repo.Object, _userRepo.Object, _currentUser.Object, _notifications.Object, _discord.Object);

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

        _notifications.Setup(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _currentUser.Setup(s => s.UserId).Returns(userId);

        var result = await BuildHandler().Handle(
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

        var result = await BuildHandler().Handle(
            new AddTicketCommentCommand(Guid.NewGuid(), "A comment"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ClosedTicket_ReturnsFailure()
    {
        var ticketId = Guid.NewGuid();
        var ticket = new Ticket { Id = ticketId, Status = TicketStatus.Closed };
        _repo.Setup(r => r.GetByIdAsync(ticketId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(ticket);
        _currentUser.Setup(s => s.UserId).Returns(Guid.NewGuid());

        var result = await BuildHandler().Handle(
            new AddTicketCommentCommand(ticketId, "Too late"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("закрыта");
    }
}
