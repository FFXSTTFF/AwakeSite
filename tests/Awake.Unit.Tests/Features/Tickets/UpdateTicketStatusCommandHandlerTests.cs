using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Tickets.Commands.UpdateTicketStatus;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Tickets;

public class UpdateTicketStatusCommandHandlerTests
{
    private readonly Mock<ITicketRepository> _repo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IDiscordNotifier> _discordNotifier = new();
    private readonly Mock<IDiscordBotService> _discordBot = new();
    private readonly Mock<INotificationService> _notifications = new();

    private UpdateTicketStatusCommandHandler BuildHandler() =>
        new(_repo.Object, _userRepo.Object, _currentUser.Object,
            _discordNotifier.Object, _discordBot.Object, _notifications.Object);

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
        _currentUser.Setup(s => s.IsAuthenticated).Returns(true);
        _discordNotifier.Setup(d => d.NotifyTicketDecisionAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);
        _notifications.Setup(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await BuildHandler().Handle(
            new UpdateTicketStatusCommand(ticket.Id, TicketStatus.Approved), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketStatus.Approved);
        ticket.ReviewedBy.Should().Be(reviewerId);
        ticket.ReviewedAt.Should().NotBeNull();
        _discordNotifier.Verify(d => d.NotifyTicketDecisionAsync(ticket, It.IsAny<CancellationToken>()), Times.Once);
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
        _currentUser.Setup(s => s.IsAuthenticated).Returns(true);

        var result = await BuildHandler().Handle(
            new UpdateTicketStatusCommand(ticket.Id, TicketStatus.InReview), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _discordNotifier.Verify(d => d.NotifyTicketDecisionAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TicketNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Ticket?)null);
        _currentUser.Setup(s => s.UserId).Returns(Guid.NewGuid());

        var result = await BuildHandler().Handle(
            new UpdateTicketStatusCommand(Guid.NewGuid(), TicketStatus.InReview), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ApproveRecruitment_PromotesGuestAuthorToMember()
    {
        var author = new User { Id = Guid.NewGuid(), Rank = UserRank.Guest, GameNickname = null };
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(), Status = TicketStatus.InReview, Type = TicketType.Recruitment,
            AuthorId = author.Id, GameNickname = "Eve",
        };

        _repo.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _userRepo.Setup(r => r.GetByIdAsync(author.Id, It.IsAny<CancellationToken>())).ReturnsAsync(author);
        _currentUser.Setup(s => s.UserId).Returns(Guid.NewGuid());
        _currentUser.Setup(s => s.IsAuthenticated).Returns(true);

        var result = await BuildHandler().Handle(
            new UpdateTicketStatusCommand(ticket.Id, TicketStatus.Approved), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        author.Rank.Should().Be(UserRank.Member);
        author.GameNickname.Should().Be("Eve");
        _userRepo.Verify(r => r.UpdateAsync(author, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ApproveRecruitment_FindsApplicantByDiscordId()
    {
        var applicant = new User { Id = Guid.NewGuid(), Rank = UserRank.Guest };
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(), Status = TicketStatus.InReview, Type = TicketType.Recruitment,
            AuthorId = null, DiscordUserId = "discord-123", GameNickname = "Frank",
        };

        _repo.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _userRepo.Setup(r => r.GetByDiscordUserIdAsync("discord-123", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(applicant);
        _currentUser.Setup(s => s.UserId).Returns(Guid.NewGuid());
        _currentUser.Setup(s => s.IsAuthenticated).Returns(true);

        await BuildHandler().Handle(
            new UpdateTicketStatusCommand(ticket.Id, TicketStatus.Approved), CancellationToken.None);

        applicant.Rank.Should().Be(UserRank.Member);
        _userRepo.Verify(r => r.UpdateAsync(applicant, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ApproveRecruitment_DoesNotTouchNonGuest()
    {
        var officer = new User { Id = Guid.NewGuid(), Rank = UserRank.Officer, GameNickname = "Old" };
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(), Status = TicketStatus.InReview, Type = TicketType.Recruitment,
            AuthorId = officer.Id, GameNickname = "New",
        };

        _repo.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _userRepo.Setup(r => r.GetByIdAsync(officer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(officer);
        _currentUser.Setup(s => s.UserId).Returns(Guid.NewGuid());
        _currentUser.Setup(s => s.IsAuthenticated).Returns(true);

        await BuildHandler().Handle(
            new UpdateTicketStatusCommand(ticket.Id, TicketStatus.Approved), CancellationToken.None);

        officer.Rank.Should().Be(UserRank.Officer);
        officer.GameNickname.Should().Be("Old");
        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ApproveAppeal_DoesNotPromote()
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(), Status = TicketStatus.InReview, Type = TicketType.Appeal,
            AuthorId = Guid.NewGuid(), GameNickname = "Grace",
        };

        _repo.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _currentUser.Setup(s => s.UserId).Returns(Guid.NewGuid());
        _currentUser.Setup(s => s.IsAuthenticated).Returns(true);

        await BuildHandler().Handle(
            new UpdateTicketStatusCommand(ticket.Id, TicketStatus.Approved), CancellationToken.None);

        _userRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DiscordBot_ReviewedByNull_WhenNotAuthenticated()
    {
        var ticket = new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Pending, GameNickname = "Dave" };
        _repo.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(ticket);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);
        _currentUser.Setup(s => s.IsAuthenticated).Returns(false);
        _currentUser.Setup(s => s.UserId).Returns(Guid.Empty);

        await BuildHandler().Handle(
            new UpdateTicketStatusCommand(ticket.Id, TicketStatus.InReview, "discord_officer"), CancellationToken.None);

        ticket.ReviewedBy.Should().BeNull();
    }
}
