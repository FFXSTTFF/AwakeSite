using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Tickets.Queries.GetTickets;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using Moq;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.Tickets;

public class GetTicketsQueryHandlerTests
{
    private readonly Mock<ITicketRepository> _repo = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    [Fact]
    public async Task Handle_OfficerUser_ReturnsAllTickets()
    {
        var author = new User { Id = Guid.NewGuid(), Username = "alice" };
        var tickets = new List<Ticket>
        {
            new() { Id = Guid.NewGuid(), AuthorId = author.Id, Author = author,
                    GameNickname = "AliceInGame", Type = TicketType.Recruitment,
                    Status = TicketStatus.Pending, Description = "Hi" }
        };

        _currentUser.Setup(s => s.UserId).Returns(Guid.NewGuid());
        _currentUser.Setup(s => s.Rank).Returns(UserRank.Officer);
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(tickets);

        var handler = new GetTicketsQueryHandler(_repo.Object, _currentUser.Object);
        var result = await handler.Handle(new GetTicketsQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].GameNickname.Should().Be("AliceInGame");
        _repo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.GetByAuthorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NonOfficerUser_ReturnsOwnTicketsOnly()
    {
        var userId = Guid.NewGuid();
        var author = new User { Id = userId, Username = "bob" };
        var tickets = new List<Ticket>
        {
            new() { Id = Guid.NewGuid(), AuthorId = userId, Author = author,
                    GameNickname = "BobInGame", Type = TicketType.Appeal,
                    Status = TicketStatus.Pending, Description = "Appeal" }
        };

        _currentUser.Setup(s => s.UserId).Returns(userId);
        _currentUser.Setup(s => s.Rank).Returns(UserRank.Member);
        _repo.Setup(r => r.GetByAuthorAsync(userId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(tickets);

        var handler = new GetTicketsQueryHandler(_repo.Object, _currentUser.Object);
        var result = await handler.Handle(new GetTicketsQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].AuthorUsername.Should().Be("bob");
        _repo.Verify(r => r.GetByAuthorAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
