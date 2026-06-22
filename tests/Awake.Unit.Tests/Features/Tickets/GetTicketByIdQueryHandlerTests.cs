using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Application.Features.Tickets.Queries.GetTicketById;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using Moq;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.Tickets;

public class GetTicketByIdQueryHandlerTests
{
    private readonly Mock<ITicketRepository> _repo = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IPlayerDataAggregator> _playerData = new();

    private Ticket MakeTicket(Guid authorId, string authorName = "alice")
    {
        var author = new User { Id = authorId, Username = authorName };
        return new Ticket
        {
            Id = Guid.NewGuid(), AuthorId = authorId, Author = author,
            GameNickname = "AliceGame", Type = TicketType.Recruitment,
            Status = TicketStatus.Pending, Description = "I want to join",
            Comments = []
        };
    }

    [Fact]
    public async Task Handle_AuthorCanSeeOwnTicket()
    {
        var userId = Guid.NewGuid();
        var ticket = MakeTicket(userId);
        _repo.Setup(r => r.GetByIdWithDetailsAsync(ticket.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(ticket);
        _currentUser.Setup(s => s.UserId).Returns(userId);
        _currentUser.Setup(s => s.Rank).Returns(UserRank.Member);

        var handler = new GetTicketByIdQueryHandler(_repo.Object, _currentUser.Object, _playerData.Object);
        var result = await handler.Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.GameNickname.Should().Be("AliceGame");
        _playerData.Verify(p => p.GetPlayerDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OfficerGetsPlayerData()
    {
        var authorId = Guid.NewGuid();
        var officerId = Guid.NewGuid();
        var ticket = MakeTicket(authorId);
        var playerResult = new PlayerDataResult("AliceGame", []);

        _repo.Setup(r => r.GetByIdWithDetailsAsync(ticket.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(ticket);
        _currentUser.Setup(s => s.UserId).Returns(officerId);
        _currentUser.Setup(s => s.Rank).Returns(UserRank.Officer);
        _playerData.Setup(p => p.GetPlayerDataAsync("AliceGame", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(playerResult);

        var handler = new GetTicketByIdQueryHandler(_repo.Object, _currentUser.Object, _playerData.Object);
        var result = await handler.Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PlayerData.Should().NotBeNull();
        _playerData.Verify(p => p.GetPlayerDataAsync("AliceGame", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NonOfficerCannotSeeOtherUsersTicket()
    {
        var authorId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var ticket = MakeTicket(authorId);

        _repo.Setup(r => r.GetByIdWithDetailsAsync(ticket.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(ticket);
        _currentUser.Setup(s => s.UserId).Returns(otherId);
        _currentUser.Setup(s => s.Rank).Returns(UserRank.Member);

        var handler = new GetTicketByIdQueryHandler(_repo.Object, _currentUser.Object, _playerData.Object);
        var result = await handler.Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_TicketNotFound_ReturnsFailure()
    {
        var ticketId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdWithDetailsAsync(ticketId, It.IsAny<CancellationToken>()))
             .ReturnsAsync((Ticket?)null);
        _currentUser.Setup(s => s.UserId).Returns(Guid.NewGuid());
        _currentUser.Setup(s => s.Rank).Returns(UserRank.Member);

        var handler = new GetTicketByIdQueryHandler(_repo.Object, _currentUser.Object, _playerData.Object);
        var result = await handler.Handle(new GetTicketByIdQuery(ticketId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
