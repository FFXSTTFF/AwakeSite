using Awake.Application.Features.Inventory.Dtos;
using Awake.Application.Features.Items.Dtos;

namespace Awake.Application.Features.Inventory;

public static class PlayerFlagsCalculator
{
    private const string CombinedArmor = "armor/combined";
    private const string CombatArmor = "armor/combat";
    private const string SniperRifle = "weapon/sniper_rifle";
    private static readonly string[] BioQualities = ["RANK_MASTER", "RANK_LEGEND"];

    public static PlayerFlagsDto Calculate(
        IEnumerable<ItemDto> knownItems, bool hasSpeedProof, bool hasVitalityProof)
    {
        var items = knownItems.ToList();
        return new PlayerFlagsDto(
            Bio: items.Any(i => i.Category == CombinedArmor && BioQualities.Contains(i.Color)),
            Combat: items.Any(i => i.Category == CombatArmor),
            Sniper: items.Any(i => i.Category == SniperRifle),
            Speed: hasSpeedProof,
            Vitality: hasVitalityProof);
    }
}
