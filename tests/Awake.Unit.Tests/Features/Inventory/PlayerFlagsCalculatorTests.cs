using Awake.Application.Features.Inventory;
using Awake.Application.Features.Items.Dtos;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.Inventory;

public class PlayerFlagsCalculatorTests
{
    private static ItemDto Item(string category, string color = "") =>
        new("id-" + Guid.NewGuid().ToString("N")[..6], category, "Предмет", "icon.png", color);

    [Theory]
    [InlineData("RANK_MASTER", true)]
    [InlineData("RANK_LEGEND", true)]
    [InlineData("RANK_VETERAN", false)] // комбинированная, но не мастерка/легенда
    [InlineData("DEFAULT", false)]
    public void Bio_RequiresCombinedArmor_MasterOrLegend(string color, bool expected)
    {
        var flags = PlayerFlagsCalculator.Calculate([Item("armor/combined", color)], false, false);
        flags.Bio.Should().Be(expected);
    }

    [Fact]
    public void Combat_AnyQualityCombatArmor()
    {
        var flags = PlayerFlagsCalculator.Calculate([Item("armor/combat", "RANK_NEWBIE")], false, false);
        flags.Combat.Should().BeTrue();
        flags.Bio.Should().BeFalse();
    }

    [Fact]
    public void Sniper_SniperRifleCategory()
    {
        var flags = PlayerFlagsCalculator.Calculate([Item("weapon/sniper_rifle")], false, false);
        flags.Sniper.Should().BeTrue();
    }

    [Fact]
    public void SpeedAndVitality_ComeFromProofs()
    {
        var flags = PlayerFlagsCalculator.Calculate([], hasSpeedProof: true, hasVitalityProof: true);
        flags.Speed.Should().BeTrue();
        flags.Vitality.Should().BeTrue();
        flags.Bio.Should().BeFalse();
    }

    [Fact]
    public void EmptyInventory_AllFalse()
    {
        var flags = PlayerFlagsCalculator.Calculate([], false, false);
        flags.Should().Be(new Awake.Application.Features.Inventory.Dtos.PlayerFlagsDto(
            false, false, false, false, false));
    }

    [Fact]
    public void ScientistArmor_DoesNotGiveBio()
    {
        // научная броня даёт биозащиту в игре, но по спеке био-флаг — только комбинированная
        var flags = PlayerFlagsCalculator.Calculate([Item("armor/scientist", "RANK_MASTER")], false, false);
        flags.Bio.Should().BeFalse();
    }
}
