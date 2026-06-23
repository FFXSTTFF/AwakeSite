using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Tickets.Commands.CreateTicket;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using Moq;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.Tickets;

public class CreateTicketCommandHandlerTests
{
    private readonly Mock<ITicketRepository> _repo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IDiscordNotifier> _discord = new();

    [Fact]
    public async Task Handle_ValidCommand_CreatesTicketAndNotifies()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Username = "tester" };
        _currentUser.Setup(s => s.UserId).Returns(userId);
        _userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);

        Ticket? savedTicket = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
             .Callback<Ticket, CancellationToken>((t, _) => savedTicket = t)
             .Returns(Task.CompletedTask);

        _discord.Setup(d => d.NotifyNewTicketAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var handler = new CreateTicketCommandHandler(_repo.Object, _userRepo.Object, _currentUser.Object, _discord.Object);
        var command = new CreateTicketCommand("AliceInGame", TicketType.Recruitment, "I want to join the clan.");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AuthorUsername.Should().Be("tester");
        savedTicket!.AuthorId.Should().Be(userId);
        savedTicket.Status.Should().Be(TicketStatus.Pending);
        savedTicket.GameNickname.Should().Be("AliceInGame");
        _discord.Verify(d => d.NotifyNewTicketAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
