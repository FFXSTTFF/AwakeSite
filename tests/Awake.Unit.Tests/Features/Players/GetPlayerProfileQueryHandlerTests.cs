using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Players.Queries.GetPlayerProfile;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using Awake.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Players;

public class GetPlayerProfileQueryHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ISquadRepository> _squads = new();
    private readonly Mock<ITicketRepository> _tickets = new();
    private readonly Mock<IPlayerStatsSnapshotRepository> _snapshots = new();
    private readonly Mock<IPlayerBoostRequestRepository> _boosts = new();
    private readonly Mock<IItemCacheService> _cache = new();

    private GetPlayerProfileQueryHandler BuildHandler() =>
        new(_users.Object, _squads.Object, _tickets.Object, _snapshots.Object, _boosts.Object, _cache.Object);

    private static User MakeUser(Guid id) => new()
    {
        Id = id, Username = "OopsITry", Rank = UserRank.Member,
        GameNickname = "OopsITry", DiscordUsername = "oops",
        DiscordAvatarUrl = "https://cdn.discordapp.com/avatars/1/a.png",
    };

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _users.Setup(u => u.GetByIdAsync(id, It.IsAny<CancellationToken>()))
              .ReturnsAsync((User?)null);

        var result = await BuildHandler().Handle(new GetPlayerProfileQuery(id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_FullProfile_MapsEverything()
    {
        var id = Guid.NewGuid();
        var user = MakeUser(id);
        _users.Setup(u => u.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var squad = new Squad { Name = "Alpha", Number = 1 };
        _squads.Setup(s => s.GetMembershipByUserIdAsync(id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new SquadMember { Squad = squad, SquadId = squad.Id, UserId = id, IsLeader = true });

        var loadout = new Loadout(null,
            new LoadoutSlot("w1", "AK-74", "icon", 5),
            new LoadoutSlot("a1", "Armor", "icon", 3));
        _tickets.Setup(t => t.GetByAuthorAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new Ticket { AuthorId = id, GameNickname = "OopsITry", Loadout = loadout }]);

        _snapshots.Setup(s => s.GetByNicknameAsync("OopsITry", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PlayerStatsSnapshot
                  {
                      GameNickname = "OopsITry", Kills = 100, Deaths = 50, KdRatio = 2.0,
                      Accuracy = "45%", Playtime = "10 days",
                      ClanHistory = [new ClanEntry("Awake", "LOVE", "")],
                      FetchedAt = DateTime.UtcNow,
                  });
        _boosts.Setup(b => b.GetByUserIdAsync(id, It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);

        var result = await BuildHandler().Handle(new GetPlayerProfileQuery(id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!;
        dto.Squad!.Name.Should().Be("Alpha");
        dto.Squad.IsLeader.Should().BeTrue();
        dto.Stats!.Kills.Should().Be(100);
        dto.Stats.ClanHistory.Should().HaveCount(1);
        dto.Loadout!.Weapon.ItemName.Should().Be("AK-74");
    }

    [Fact]
    public async Task Handle_NoSquadNoStatsNoLoadout_ReturnsNulls()
    {
        var id = Guid.NewGuid();
        var user = MakeUser(id);
        user.GameNickname = null;
        _users.Setup(u => u.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _squads.Setup(s => s.GetMembershipByUserIdAsync(id, It.IsAny<CancellationToken>()))
               .ReturnsAsync((SquadMember?)null);
        _tickets.Setup(t => t.GetByAuthorAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        _boosts.Setup(b => b.GetByUserIdAsync(id, It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);

        var result = await BuildHandler().Handle(new GetPlayerProfileQuery(id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Squad.Should().BeNull();
        result.Value.Stats.Should().BeNull();
        result.Value.Loadout.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UserLoadout_PreferredOverTicketLoadout()
    {
        var id = Guid.NewGuid();
        var user = MakeUser(id);
        user.GameNickname = null;
        user.Loadout = new Loadout(null,
            new LoadoutSlot("w2", "Гроза", "icon", 8),
            new LoadoutSlot("a2", "СЕВА", "icon", 4));
        _users.Setup(u => u.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _squads.Setup(s => s.GetMembershipByUserIdAsync(id, It.IsAny<CancellationToken>()))
               .ReturnsAsync((SquadMember?)null);
        _tickets.Setup(t => t.GetByAuthorAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new Ticket
                {
                    AuthorId = id, GameNickname = "OopsITry",
                    Loadout = new Loadout(null,
                        new LoadoutSlot("w1", "АК из заявки", "icon", 0),
                        new LoadoutSlot("a1", "Броня из заявки", "icon", 0)),
                }]);
        _boosts.Setup(b => b.GetByUserIdAsync(id, It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);

        var result = await BuildHandler().Handle(new GetPlayerProfileQuery(id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Loadout!.Weapon.ItemName.Should().Be("Гроза");
    }
}
