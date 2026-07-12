using Awake.Domain.Common;
using Awake.Domain.ValueObjects;

namespace Awake.Domain.Entities;

// Снапшот игровой статистики. Ключ — игровой ник, а не UserId:
// снапшот создаётся при Discord-заявке, когда аккаунта на сайте ещё нет.
public class PlayerStatsSnapshot : BaseEntity
{
    public string GameNickname { get; set; } = string.Empty;
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public double KdRatio { get; set; }
    public string Accuracy { get; set; } = "—";
    public string Playtime { get; set; } = "—";
    public List<ClanEntry> ClanHistory { get; set; } = [];
    public DateTime FetchedAt { get; set; }
}
