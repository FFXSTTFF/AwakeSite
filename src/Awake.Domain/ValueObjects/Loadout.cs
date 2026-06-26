namespace Awake.Domain.ValueObjects;

public record Loadout(
    LoadoutSlot? Sniper,
    LoadoutSlot Weapon,
    LoadoutSlot Armor
);
