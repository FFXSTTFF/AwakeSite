using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Tickets.Commands.CreateTicket;
using Awake.Application.Features.Tickets.Dtos;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Tickets;

public class CreateTicketCommandHandlerTests
{
    private readonly Mock<ITicketRepository> _repo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IDiscordNotifier> _discord = new();
    private readonly Mock<INotificationService> _notifications = new();

    private CreateTicketCommandHandler BuildHandler() =>
        new(_repo.Object, _userRepo.Object, _currentUser.Object, _discord.Object, _notifications.Object);

    private void SetupUser(Guid userId, string username)
    {
        var user = new User { Id = userId, Username = username };
        _currentUser.Setup(s => s.UserId).Returns(userId);
        _userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _repo.Setup(r => r.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);
        _discord.Setup(d => d.NotifyNewTicketAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        _notifications.Setup(n => n.CreateForRankAsync(
            It.IsAny<UserRank>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesTicketAndNotifies()
    {
        var userId = Guid.NewGuid();
        SetupUser(userId, "tester");

        Ticket? savedTicket = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
             .Callback<Ticket, CancellationToken>((t, _) => savedTicket = t)
             .Returns(Task.CompletedTask);

        var command = new CreateTicketCommand("AliceInGame", TicketType.Recruitment, "I want to join.", null);
        var result = await BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AuthorUsername.Should().Be("tester");
        savedTicket!.AuthorId.Should().Be(userId);
        savedTicket.Status.Should().Be(TicketStatus.Pending);
        savedTicket.GameNickname.Should().Be("AliceInGame");
        savedTicket.Loadout.Should().BeNull();
        _discord.Verify(d => d.NotifyNewTicketAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CommandWithLoadout_SavesLoadoutOnTicket()
    {
        var userId = Guid.NewGuid();
        SetupUser(userId, "tester");

        Ticket? savedTicket = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
             .Callback<Ticket, CancellationToken>((t, _) => savedTicket = t)
             .Returns(Task.CompletedTask);

        var loadout = new Awake.Domain.ValueObjects.Loadout(
            Sniper: null,
            Weapon: new Awake.Domain.ValueObjects.LoadoutSlot("w1", "АК-74М", "https://example.com/ak.png"),
            Armor: new Awake.Domain.ValueObjects.LoadoutSlot("a1", "Страж", "https://example.com/armor.png")
        );

        var command = new CreateTicketCommand("AliceInGame", TicketType.Recruitment, "I want to join.", loadout);
        var result = await BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        savedTicket!.Loadout.Should().NotBeNull();
        savedTicket.Loadout!.Weapon.ItemName.Should().Be("АК-74М");
        savedTicket.Loadout.Sniper.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        _currentUser.Setup(s => s.UserId).Returns(Guid.NewGuid());
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((User?)null);

        var command = new CreateTicketCommand("Nick", TicketType.Recruitment, "desc", null);
        var result = await BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
