using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Auth.Commands.SyncDiscordRoles;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Auth;

public class SyncDiscordRolesCommandHandlerTests
{
    private const string DiscordId = "444555666";
    private const string GuildId = "999888777";
    private const string MemberRole = "role-member";
    private const string OfficerRole = "role-officer";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDiscordRoleSyncSettings> _settings = new();
    private readonly Mock<IDiscordBotService> _bot = new();
    private readonly Mock<INotificationService> _notifications = new();

    public SyncDiscordRolesCommandHandlerTests()
    {
        _settings.Setup(s => s.Enabled).Returns(true);
        _settings.Setup(s => s.GuildId).Returns(GuildId);
        _settings.Setup(s => s.RoleToRank).Returns(new Dictionary<string, UserRank>
        {
            [MemberRole] = UserRank.Member,
            [OfficerRole] = UserRank.Officer,
        });
        _notifications.Setup(n => n.CreateAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private SyncDiscordRolesCommandHandler BuildHandler() =>
        new(_users.Object, _settings.Object, _bot.Object, _notifications.Object);

    private User SetupUser(UserRank rank)
    {
        var user = new User { Id = Guid.NewGuid(), Rank = rank, DiscordUserId = DiscordId };
        _users.Setup(u => u.GetByDiscordUserIdAsync(DiscordId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);
        return user;
    }

    [Fact]
    public async Task Handle_Disabled_DoesNothing()
    {
        _settings.Setup(s => s.Enabled).Returns(false);

        var result = await BuildHandler().Handle(
            new SyncDiscordRolesCommand(DiscordId, [MemberRole]), CancellationToken.None);

        result.Value.Should().BeFalse();
        _users.Verify(u => u.GetByDiscordUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MappedRole_PromotesGuestToMember()
    {
        var user = SetupUser(UserRank.Guest);

        var result = await BuildHandler().Handle(
            new SyncDiscordRolesCommand(DiscordId, [MemberRole, "unmapped-role"]), CancellationToken.None);

        result.Value.Should().BeTrue();
        user.Rank.Should().Be(UserRank.Member);
        _users.Verify(u => u.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(n => n.CreateAsync(user.Id, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MultipleMappedRoles_HighestWins()
    {
        var user = SetupUser(UserRank.Guest);

        await BuildHandler().Handle(
            new SyncDiscordRolesCommand(DiscordId, [MemberRole, OfficerRole]), CancellationToken.None);

        user.Rank.Should().Be(UserRank.Officer);
    }

    [Fact]
    public async Task Handle_NoMappedRoles_DemotesMemberToGuest()
    {
        var user = SetupUser(UserRank.Member);

        var result = await BuildHandler().Handle(
            new SyncDiscordRolesCommand(DiscordId, ["unmapped-role"]), CancellationToken.None);

        result.Value.Should().BeTrue();
        user.Rank.Should().Be(UserRank.Guest);
        // понижение — без поздравительного уведомления
        _notifications.Verify(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_LeaderUser_NeverTouched()
    {
        var user = SetupUser(UserRank.Leader);

        var result = await BuildHandler().Handle(
            new SyncDiscordRolesCommand(DiscordId, ["unmapped-role"]), CancellationToken.None);

        result.Value.Should().BeFalse();
        user.Rank.Should().Be(UserRank.Leader);
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MappingWithLeader_CappedAtColonel()
    {
        // Кривой конфиг: роль замаплена на Leader — синк не должен его выдать
        _settings.Setup(s => s.RoleToRank).Returns(new Dictionary<string, UserRank>
        {
            ["role-leader"] = UserRank.Leader,
        });
        var user = SetupUser(UserRank.Guest);

        await BuildHandler().Handle(
            new SyncDiscordRolesCommand(DiscordId, ["role-leader"]), CancellationToken.None);

        user.Rank.Should().Be(UserRank.Colonel);
    }

    [Fact]
    public async Task Handle_RankUnchanged_NoUpdate()
    {
        var user = SetupUser(UserRank.Member);

        var result = await BuildHandler().Handle(
            new SyncDiscordRolesCommand(DiscordId, [MemberRole]), CancellationToken.None);

        result.Value.Should().BeFalse();
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NullRoleIds_FetchesFromRest()
    {
        var user = SetupUser(UserRank.Guest);
        _bot.Setup(b => b.GetGuildMemberRoleIdsAsync(GuildId, DiscordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([MemberRole]);

        await BuildHandler().Handle(
            new SyncDiscordRolesCommand(DiscordId, RoleIds: null), CancellationToken.None);

        user.Rank.Should().Be(UserRank.Member);
    }

    [Fact]
    public async Task Handle_RestReturnsNull_RankNotTouched()
    {
        // Не участник сервера или REST недоступен — не понижаем
        var user = SetupUser(UserRank.Member);
        _bot.Setup(b => b.GetGuildMemberRoleIdsAsync(GuildId, DiscordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string>?)null);

        var result = await BuildHandler().Handle(
            new SyncDiscordRolesCommand(DiscordId, RoleIds: null), CancellationToken.None);

        result.Value.Should().BeFalse();
        user.Rank.Should().Be(UserRank.Member);
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownUser_DoesNothing()
    {
        _users.Setup(u => u.GetByDiscordUserIdAsync(DiscordId, It.IsAny<CancellationToken>()))
              .ReturnsAsync((User?)null);

        var result = await BuildHandler().Handle(
            new SyncDiscordRolesCommand(DiscordId, [MemberRole]), CancellationToken.None);

        result.Value.Should().BeFalse();
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
