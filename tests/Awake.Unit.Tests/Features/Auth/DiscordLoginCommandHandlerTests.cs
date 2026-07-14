using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Application.Features.Auth.Commands.DiscordLogin;
using Awake.Application.Features.Auth.Commands.SyncDiscordRoles;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using MediatR;
using Moq;

namespace Awake.Unit.Tests.Features.Auth;

public class DiscordLoginCommandHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ITicketRepository> _tickets = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<ISender> _sender = new();

    private static readonly DiscordUserInfo Info =
        new("111222333", "oops", "OopsITry", "https://cdn.discordapp.com/avatars/111222333/a.png");

    private DiscordLoginCommandHandler BuildHandler() =>
        new(_users.Object, _tickets.Object, _tokens.Object, _sender.Object);

    public DiscordLoginCommandHandlerTests()
    {
        _tokens.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("jwt");
        _tickets.Setup(t => t.GetUnlinkedByDiscordUserIdAsync("111222333", It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        _sender.Setup(s => s.Send(It.IsAny<SyncDiscordRolesCommand>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(Result<bool>.Success(false));
    }

    [Fact]
    public async Task Handle_SendsDiscordRoleSync_BeforeIssuingToken()
    {
        _users.Setup(u => u.GetByDiscordUserIdAsync("111222333", It.IsAny<CancellationToken>()))
              .ReturnsAsync(new User { Id = Guid.NewGuid(), DiscordUserId = "111222333", Username = "x" });

        await BuildHandler().Handle(new DiscordLoginCommand(Info), CancellationToken.None);

        _sender.Verify(s => s.Send(
            It.Is<SyncDiscordRolesCommand>(c => c.DiscordUserId == "111222333" && c.RoleIds == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NewDiscordUser_CreatesGuestAccount()
    {
        _users.Setup(u => u.GetByDiscordUserIdAsync("111222333", It.IsAny<CancellationToken>()))
              .ReturnsAsync((User?)null);
        User? saved = null;
        _users.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
              .Callback<User, CancellationToken>((u, _) => saved = u)
              .Returns(Task.CompletedTask);

        var result = await BuildHandler().Handle(new DiscordLoginCommand(Info), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        saved!.DiscordUserId.Should().Be("111222333");
        saved.Username.Should().Be("OopsITry");   // global_name предпочтительнее username
        saved.Rank.Should().Be(UserRank.Guest);
        saved.DiscordAvatarUrl.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_NewUser_UsernameTaken_DisambiguatesWithIdSuffix()
    {
        _users.Setup(u => u.GetByDiscordUserIdAsync("111222333", It.IsAny<CancellationToken>()))
              .ReturnsAsync((User?)null);
        // "OopsITry" уже занят другим пользователем
        _users.Setup(u => u.ExistsByUsernameAsync("OopsITry", It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);
        User? saved = null;
        _users.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
              .Callback<User, CancellationToken>((u, _) => saved = u)
              .Returns(Task.CompletedTask);

        var result = await BuildHandler().Handle(new DiscordLoginCommand(Info), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        saved!.Username.Should().Be("OopsITry_2333");   // последние 4 символа Discord id
    }

    [Fact]
    public async Task Handle_NewUser_SuffixedUsernameAlsoTaken_FallsBackToFullId()
    {
        _users.Setup(u => u.GetByDiscordUserIdAsync("111222333", It.IsAny<CancellationToken>()))
              .ReturnsAsync((User?)null);
        _users.Setup(u => u.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);
        User? saved = null;
        _users.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
              .Callback<User, CancellationToken>((u, _) => saved = u)
              .Returns(Task.CompletedTask);

        var result = await BuildHandler().Handle(new DiscordLoginCommand(Info), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        saved!.Username.Should().Be("OopsITry_111222333");   // полный id глобально уникален
    }

    [Fact]
    public async Task Handle_ExistingUser_LogsInWithoutCreating()
    {
        var user = new User { Username = "OopsITry", DiscordUserId = "111222333", Rank = UserRank.Member };
        _users.Setup(u => u.GetByDiscordUserIdAsync("111222333", It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

        var result = await BuildHandler().Handle(new DiscordLoginCommand(Info), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Rank.Should().Be(UserRank.Member);
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_HangingTickets_LinksThemAndCopiesNickname()
    {
        var user = new User { DiscordUserId = "111222333", Username = "OopsITry" };
        _users.Setup(u => u.GetByDiscordUserIdAsync("111222333", It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

        var older = new Ticket { DiscordUserId = "111222333", GameNickname = "OldNick",
            CreatedAt = DateTime.UtcNow.AddDays(-2) };
        var newer = new Ticket { DiscordUserId = "111222333", GameNickname = "FreshNick",
            CreatedAt = DateTime.UtcNow };
        // Репозиторий возвращает по убыванию CreatedAt
        _tickets.Setup(t => t.GetUnlinkedByDiscordUserIdAsync("111222333", It.IsAny<CancellationToken>()))
                .ReturnsAsync([newer, older]);

        await BuildHandler().Handle(new DiscordLoginCommand(Info), CancellationToken.None);

        newer.AuthorId.Should().Be(user.Id);
        older.AuthorId.Should().Be(user.Id);
        user.GameNickname.Should().Be("FreshNick");
        _tickets.Verify(t => t.UpdateAsync(newer, It.IsAny<CancellationToken>()), Times.Once);
        _tickets.Verify(t => t.UpdateAsync(older, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoHangingTickets_IsIdempotent()
    {
        var user = new User { DiscordUserId = "111222333", Username = "OopsITry", GameNickname = "KeepMe" };
        _users.Setup(u => u.GetByDiscordUserIdAsync("111222333", It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

        await BuildHandler().Handle(new DiscordLoginCommand(Info), CancellationToken.None);

        user.GameNickname.Should().Be("KeepMe");
        _tickets.Verify(t => t.UpdateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
