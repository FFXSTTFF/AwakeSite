namespace Awake.Domain.Enums;

/// <summary>Типы бафов на КВ. Значения фиксированы — сериализуются числами в API.</summary>
public enum BoostType
{
    Damage = 0,       // Усиление
    ShortDamage = 1,  // Кратковременное усиление
    Speed = 2,        // Скорость
    Defense = 3,      // Защита
}
