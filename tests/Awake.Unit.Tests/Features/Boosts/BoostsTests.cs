using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Boosts;
using Awake.Application.Features.Boosts.Commands.SetMyBoosts;
using Awake.Application.Features.Boosts.Dtos;
using Awake.Application.Features.Boosts.Queries.GetBoostsSummary;
using Awake.Application.Features.Boosts.Queries.GetMyBoosts;
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Boosts;

public class BoostsTests
{
    private readonly Mock<IPlayerBoostRequestRepository> _repo = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private static readonly Guid UserId = Guid.NewGuid();

    private static ItemDto Ozverin => new("ozverin", "supply/medicine", "«Озверин»", "i.png", "RANK_VETERAN", BoostType.ShortDamage);

    // ── SetMyBoosts ──

    [Fact]
    public async Task SetMyBoosts_ValidSelection_Replaces()
    {
        _cache.Setup(c => c.GetById("ozverin")).Returns(Ozverin);
        var handler = new SetMyBoostsCommandHandler(_repo.Object, _cache.Object);

        var result = await handler.Handle(new SetMyBoostsCommand(UserId,
            [new BoostSelectionDto(BoostType.ShortDamage, "ozverin")]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.ReplaceForUserAsync(UserId,
            It.Is<IReadOnlyList<PlayerBoostRequest>>(l =>
                l.Count == 1 && l[0].ItemId == "ozverin" && l[0].BoostType == BoostType.ShortDamage),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetMyBoosts_UnknownItem_Fails()
    {
        _cache.Setup(c => c.GetById("ghost")).Returns((ItemDto?)null);
        var handler = new SetMyBoostsCommandHandler(_repo.Object, _cache.Object);

        var result = await handler.Handle(new SetMyBoostsCommand(UserId,
            [new BoostSelectionDto(BoostType.Damage, "ghost")]), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _repo.Verify(r => r.ReplaceForUserAsync(It.IsAny<Guid>(),
            It.IsAny<IReadOnlyList<PlayerBoostRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetMyBoosts_TypeMismatch_Fails()
    {
        _cache.Setup(c => c.GetById("ozverin")).Returns(Ozverin); // ShortDamage
        var handler = new SetMyBoostsCommandHandler(_repo.Object, _cache.Object);

        var result = await handler.Handle(new SetMyBoostsCommand(UserId,
            [new BoostSelectionDto(BoostType.Speed, "ozverin")]), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SetMyBoosts_LowRankItem_Fails()
    {
        _cache.Setup(c => c.GetById("soup")).Returns(
            new ItemDto("soup", "supply/food", "Суп", "i.png", "RANK_NEWBIE", BoostType.Damage));
        var handler = new SetMyBoostsCommandHandler(_repo.Object, _cache.Object);

        var result = await handler.Handle(new SetMyBoostsCommand(UserId,
            [new BoostSelectionDto(BoostType.Damage, "soup")]), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Validator_DuplicateType_Invalid()
    {
        var validator = new SetMyBoostsCommandValidator();
        var result = validator.Validate(new SetMyBoostsCommand(UserId, [
            new BoostSelectionDto(BoostType.Damage, "a"),
            new BoostSelectionDto(BoostType.Damage, "b"),
        ]));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_EmptyItemId_Invalid()
    {
        var validator = new SetMyBoostsCommandValidator();
        var result = validator.Validate(new SetMyBoostsCommand(UserId,
            [new BoostSelectionDto(BoostType.Damage, "")]));
        result.IsValid.Should().BeFalse();
    }

    // ── GetMyBoosts ──

    [Fact]
    public async Task GetMyBoosts_EnrichesFromCache_WithFallback()
    {
        _repo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
             .ReturnsAsync([
                 new PlayerBoostRequest { UserId = UserId, BoostType = BoostType.ShortDamage, ItemId = "ozverin" },
                 new PlayerBoostRequest { UserId = UserId, BoostType = BoostType.Speed, ItemId = "gone" },
             ]);
        _cache.Setup(c => c.GetById("ozverin")).Returns(Ozverin);
        _cache.Setup(c => c.GetById("gone")).Returns((ItemDto?)null);
        var handler = new GetMyBoostsQueryHandler(_repo.Object, _cache.Object);

        var result = await handler.Handle(new GetMyBoostsQuery(UserId), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("«Озверин»");
        result[1].Name.Should().Be("gone"); // исчез из кэша — фолбэк на itemId
        result[1].Icon.Should().BeNull();
    }

    // ── GetBoostsSummary ──

    [Fact]
    public async Task Summary_GroupsByUser_SortsByCountThenNickname()
    {
        var u1 = new User { Username = "b", GameNickname = "Bravo", Rank = UserRank.Member };
        var u2 = new User { Username = "a", GameNickname = "Alpha", Rank = UserRank.Member };
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([
            new PlayerBoostRequest { UserId = u1.Id, User = u1, BoostType = BoostType.Damage, ItemId = "x" },
            new PlayerBoostRequest { UserId = u1.Id, User = u1, BoostType = BoostType.Speed, ItemId = "y" },
            new PlayerBoostRequest { UserId = u2.Id, User = u2, BoostType = BoostType.Damage, ItemId = "x" },
        ]);
        _cache.Setup(c => c.GetById(It.IsAny<string>())).Returns((ItemDto?)null);
        var handler = new GetBoostsSummaryQueryHandler(_repo.Object, _cache.Object);

        var result = await handler.Handle(new GetBoostsSummaryQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].UserId.Should().Be(u1.Id); // 2 буста > 1
        result[0].Boosts.Select(b => b.BoostType).Should().Equal(BoostType.Damage, BoostType.Speed);
    }
}
