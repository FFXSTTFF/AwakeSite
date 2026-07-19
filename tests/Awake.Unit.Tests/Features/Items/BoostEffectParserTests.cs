using System.Text.Json;
using Awake.Application.Features.Items;
using Awake.Domain.Enums;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.Items;

public class BoostEffectParserTests
{
    private const string ItemJsonWithEffect = """
    {
      "infoBlocks": [
        { "type": "list", "elements": [
          { "type": "key-value",
            "key": { "type": "translation", "key": "stalker.tooltip.medicine.info.effect_type", "lines": { "ru": "Назначение" } },
            "value": { "type": "translation", "key": "item.effects.effect_type.long_time_medicine", "lines": { "ru": "Усиление" } } }
        ] }
      ]
    }
    """;

    [Fact]
    public void ExtractEffectType_FindsNestedKeyValueBlock()
    {
        using var doc = JsonDocument.Parse(ItemJsonWithEffect);
        BoostEffectParser.ExtractEffectType(doc.RootElement).Should().Be("long_time_medicine");
    }

    [Fact]
    public void ExtractEffectType_NoBlock_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""{ "infoBlocks": [] }""");
        BoostEffectParser.ExtractEffectType(doc.RootElement).Should().BeNull();
    }

    [Theory]
    [InlineData("long_time_medicine", BoostType.Damage)]
    [InlineData("short_time_medicine", BoostType.ShortDamage)]
    [InlineData("mobility", BoostType.Speed)]
    [InlineData("protection", BoostType.Defense)]
    public void MapToBoostType_KnownTypes(string effectType, BoostType expected) =>
        BoostEffectParser.MapToBoostType(effectType).Should().Be(expected);

    [Theory]
    [InlineData("healing")]
    [InlineData("accumulation")]
    [InlineData(null)]
    public void MapToBoostType_NonBoost_ReturnsNull(string? effectType) =>
        BoostEffectParser.MapToBoostType(effectType).Should().BeNull();
}
