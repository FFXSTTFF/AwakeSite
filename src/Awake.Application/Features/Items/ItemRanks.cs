namespace Awake.Application.Features.Items;

/// <summary>Цвета рангов «Ветеран и выше» — единый фильтр для поиска и валидации.</summary>
public static class ItemRanks
{
    public static readonly IReadOnlySet<string> VeteranPlus =
        new HashSet<string> { "RANK_VETERAN", "RANK_MASTER", "RANK_LEGEND" };
}
