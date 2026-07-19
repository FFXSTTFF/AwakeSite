using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Enums;
using Awake.Infrastructure.ExternalServices.Items;
using FluentAssertions;

namespace Awake.Unit.Tests.Infrastructure;

public class ItemCacheServiceTests
{
    private static ItemCacheService Loaded()
    {
        var cache = new ItemCacheService();
        cache.Load([
            new ItemDto("ozverin", "supply/medicine", "«Озверин»", "i1.png", "RANK_VETERAN", BoostType.ShortDamage),
            new ItemDto("olivie", "supply/food", "Салат оливье", "i2.png", "RANK_VETERAN", BoostType.Damage),
            new ItemDto("newbie-soup", "supply/food", "Суп новичка", "i3.png", "RANK_NEWBIE", BoostType.Damage),
            new ItemDto("topot", "supply/medicine", "«ТОПОТ»", "i4.png", "RANK_MASTER"), // healing → без типа
            new ItemDto("skif5", "armor/combined", "Скиф-5", "i5.png", "RANK_MASTER"),
        ]);
        return cache;
    }

    [Fact]
    public void SearchBoosts_FiltersByTypeAndRank()
    {
        var result = Loaded().SearchBoosts("", BoostType.Damage).ToList();
        result.Should().ContainSingle(x => x.Id == "olivie"); // newbie-soup отсечён рангом
    }

    [Fact]
    public void SearchBoosts_EmptyQuery_ReturnsAllOfType()
    {
        Loaded().SearchBoosts("", BoostType.ShortDamage).Should().ContainSingle(x => x.Id == "ozverin");
    }

    [Fact]
    public void SearchBoosts_QueryFiltersByName()
    {
        Loaded().SearchBoosts("оливье", BoostType.Damage).Should().ContainSingle(x => x.Id == "olivie");
        Loaded().SearchBoosts("зюзюблик", BoostType.Damage).Should().BeEmpty();
    }
}
