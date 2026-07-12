using Awake.Application.Features.Items.Dtos;
using Awake.Infrastructure.ExternalServices.Items;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.Items;

public class ItemCacheServiceTests
{
    private static ItemDto MakeItem(string id, string category, string name, string color) =>
        new(id, category, name, $"https://example.com/icons/{id}.png", color);

    [Fact]
    public void Search_ExcludesLowRankItems()
    {
        var svc = new ItemCacheService();
        svc.Load([
            MakeItem("a1", "weapon/sniper_rifle", "СКТ-40", "RANK_VETERAN"),
            MakeItem("a2", "weapon/sniper_rifle", "Дешёвая", "RANK_NEWBIE"),
            MakeItem("a3", "weapon/sniper_rifle", "Средняя", "RANK_STALKER"),
            MakeItem("a4", "weapon/sniper_rifle", "Дефолтная", "DEFAULT"),
        ]);

        var results = svc.Search("", null).ToList();

        results.Should().HaveCount(1);
        results[0].Id.Should().Be("a1");
    }

    [Fact]
    public void Search_AllowedColors_AllThreeRanks()
    {
        var svc = new ItemCacheService();
        svc.Load([
            MakeItem("v1", "weapon/assault_rifle", "Ветеран-оружие", "RANK_VETERAN"),
            MakeItem("m1", "weapon/assault_rifle", "Мастер-оружие", "RANK_MASTER"),
            MakeItem("l1", "weapon/assault_rifle", "Легенда-оружие", "RANK_LEGEND"),
        ]);

        var results = svc.Search("", null).ToList();

        results.Should().HaveCount(3);
    }

    [Fact]
    public void Search_FiltersByCategory()
    {
        var svc = new ItemCacheService();
        svc.Load([
            MakeItem("w1", "weapon/assault_rifle", "АК-74М", "RANK_MASTER"),
            MakeItem("s1", "weapon/sniper_rifle", "СКТ-40", "RANK_MASTER"),
            MakeItem("ar1", "armor/combat", "Страж", "RANK_MASTER"),
        ]);

        var results = svc.Search("", "weapon/sniper_rifle").ToList();

        results.Should().HaveCount(1);
        results[0].Id.Should().Be("s1");
    }

    [Fact]
    public void Search_FiltersByCategoryPrefix()
    {
        var svc = new ItemCacheService();
        svc.Load([
            MakeItem("w1", "weapon/assault_rifle", "АК-74М", "RANK_MASTER"),
            MakeItem("s1", "weapon/sniper_rifle", "СКТ-40", "RANK_MASTER"),
            MakeItem("ar1", "armor/combat", "Страж", "RANK_MASTER"),
        ]);

        var results = svc.Search("", "weapon").ToList();

        results.Should().HaveCount(2);
        results.Select(r => r.Id).Should().BeEquivalentTo(["w1", "s1"]);
    }

    [Fact]
    public void Search_ExcludesCategoryPrefix()
    {
        var svc = new ItemCacheService();
        svc.Load([
            MakeItem("w1", "weapon/assault_rifle", "АК-74М", "RANK_MASTER"),
            MakeItem("s1", "weapon/sniper_rifle", "СКТ-40", "RANK_MASTER"),
        ]);

        var results = svc.Search("", "weapon", "weapon/sniper_rifle").ToList();

        results.Should().HaveCount(1);
        results[0].Id.Should().Be("w1");
    }

    [Fact]
    public void Search_FiltersByNameQueryCaseInsensitive()
    {
        var svc = new ItemCacheService();
        svc.Load([
            MakeItem("w1", "weapon/assault_rifle", "АК-74М", "RANK_MASTER"),
            MakeItem("w2", "weapon/assault_rifle", "Страж-2", "RANK_MASTER"),
        ]);

        var results = svc.Search("ак", null).ToList();

        results.Should().HaveCount(1);
        results[0].Id.Should().Be("w1");
    }

    [Fact]
    public void Search_ReturnsMax15Results()
    {
        var svc = new ItemCacheService();
        svc.Load(Enumerable.Range(1, 20).Select(i =>
            MakeItem($"id{i}", "weapon/assault_rifle", $"Оружие {i}", "RANK_MASTER")));

        var results = svc.Search("", null).ToList();

        results.Should().HaveCount(15);
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsAllMatchingCategory()
    {
        var svc = new ItemCacheService();
        svc.Load([
            MakeItem("a1", "armor/combat", "Броня 1", "RANK_MASTER"),
            MakeItem("a2", "armor/combat", "Броня 2", "RANK_LEGEND"),
            MakeItem("w1", "weapon/assault_rifle", "Оружие", "RANK_MASTER"),
        ]);

        var results = svc.Search("", "armor").ToList();

        results.Should().HaveCount(2);
    }

    [Fact]
    public void Count_ReflectsLoadedItems()
    {
        var svc = new ItemCacheService();
        svc.Load([
            MakeItem("a1", "armor/combat", "Броня", "RANK_MASTER"),
            MakeItem("w1", "weapon/assault_rifle", "Оружие", "RANK_MASTER"),
        ]);

        svc.Count.Should().Be(2);
    }

    [Fact]
    public void Load_ReplacesExistingCache()
    {
        var svc = new ItemCacheService();
        svc.Load([MakeItem("old1", "armor/combat", "Старый", "RANK_MASTER")]);
        svc.Load([MakeItem("new1", "armor/combat", "Новый", "RANK_LEGEND")]);

        var results = svc.Search("", null).ToList();

        results.Should().HaveCount(1);
        results[0].Id.Should().Be("new1");
    }
}
