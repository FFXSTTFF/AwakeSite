using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Boosts.Commands.SetMyBoosts;
using Awake.Application.Features.Boosts.Queries.GetBoostsSummary;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Boosts;

public class BoostsTests
{
    private readonly Mock<IPlayerBoostRequestRepository> _repo = new();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task SetMyBoosts_DeduplicatesInput_AndReplaces()
    {
        var handler = new SetMyBoostsCommandHandler(_repo.Object);

        var result = await handler.Handle(new SetMyBoostsCommand(
            _userId, [BoostType.Speed, BoostType.Speed, BoostType.Damage]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.ReplaceForUserAsync(
            _userId,
            It.Is<IReadOnlyList<BoostType>>(l =>
                l.Count == 2 && l.Contains(BoostType.Speed) && l.Contains(BoostType.Damage)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetMyBoosts_EmptyList_ClearsAll()
    {
        var handler = new SetMyBoostsCommandHandler(_repo.Object);

        var result = await handler.Handle(
            new SetMyBoostsCommand(_userId, []), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.ReplaceForUserAsync(
            _userId,
            It.Is<IReadOnlyList<BoostType>>(l => l.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Validator_UnknownEnumValue_Fails()
    {
        var validator = new SetMyBoostsCommandValidator();

        var result = validator.Validate(new SetMyBoostsCommand(_userId, [(BoostType)99]));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_ValidSet_Passes()
    {
        var validator = new SetMyBoostsCommandValidator();

        var result = validator.Validate(new SetMyBoostsCommand(
            _userId, [BoostType.Damage, BoostType.ShortDamage, BoostType.Speed, BoostType.Defense]));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Summary_GroupsByUser_SortsByCountDescThenNick()
    {
        var alice = new User { Username = "alice", GameNickname = "Zorro" };   // 1 буст
        var bob = new User { Username = "bob", GameNickname = "Alpha" };       // 2 буста
        var carl = new User { Username = "carl", GameNickname = null };        // 2 буста, без ника
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new PlayerBoostRequest { UserId = alice.Id, User = alice, BoostType = BoostType.Speed },
            new PlayerBoostRequest { UserId = bob.Id, User = bob, BoostType = BoostType.Damage },
            new PlayerBoostRequest { UserId = bob.Id, User = bob, BoostType = BoostType.Defense },
            new PlayerBoostRequest { UserId = carl.Id, User = carl, BoostType = BoostType.Speed },
            new PlayerBoostRequest { UserId = carl.Id, User = carl, BoostType = BoostType.Damage },
        ]);
        var handler = new GetBoostsSummaryQueryHandler(_repo.Object);

        var result = await handler.Handle(new GetBoostsSummaryQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
        // bob (2, «Alpha») раньше carl (2, ник null -> username «carl»), alice (1) последняя
        result[0].UserId.Should().Be(bob.Id);
        result[1].UserId.Should().Be(carl.Id);
        result[2].UserId.Should().Be(alice.Id);
        result[0].BoostTypes.Should().BeEquivalentTo([BoostType.Damage, BoostType.Defense]);
    }
}
