using System.Text.Json;
using Awake.Domain.Enums;

namespace Awake.Application.Features.Items;

/// <summary>
/// Вычитывает «Назначение» (effect_type) из JSON предмета stalzone-database
/// и маппит его на тип буста. Структура блока — key-value с ключом
/// "*.effect_type" и значением "item.effects.effect_type.<тип>".
/// </summary>
public static class BoostEffectParser
{
    public static string? ExtractEffectType(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("type", out var type) && type.ValueEqual("key-value")
                && root.TryGetProperty("key", out var key)
                && key.TryGetProperty("key", out var keyKey)
                && (keyKey.GetString()?.EndsWith(".effect_type") ?? false)
                && root.TryGetProperty("value", out var value)
                && value.TryGetProperty("key", out var valueKey))
            {
                var full = valueKey.GetString();
                return full?[(full.LastIndexOf('.') + 1)..];
            }
            foreach (var prop in root.EnumerateObject())
            {
                var found = ExtractEffectType(prop.Value);
                if (found is not null) return found;
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in root.EnumerateArray())
            {
                var found = ExtractEffectType(el);
                if (found is not null) return found;
            }
        }
        return null;
    }

    private static bool ValueEqual(this JsonElement el, string expected) =>
        el.ValueKind == JsonValueKind.String && el.GetString() == expected;

    public static BoostType? MapToBoostType(string? effectType) => effectType switch
    {
        "long_time_medicine" => BoostType.Damage,
        "short_time_medicine" => BoostType.ShortDamage,
        "mobility" => BoostType.Speed,
        "protection" => BoostType.Defense,
        _ => null,
    };
}
