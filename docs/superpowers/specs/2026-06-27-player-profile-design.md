# Player Profile Design

## Goal

Show real STALCRAFT player statistics (kills, deaths, K/D, accuracy, playtime, clan history) on the ticket detail page by scraping stalcrafthq.com and caching results with stale-while-revalidate.

## Context

The clan (Awake [LOVE]) operates exclusively on the EU server. The clan has ~30–35 members, so the profile cache will hold at most 35 entries at a time. Player profile data is displayed inside the existing "Данные игрока" card on the ticket detail page — no separate profile page.

---

## Data Source

**Site:** `https://stalcrafthq.com/characters/EU/{nickname}`

**Server:** always `EU` — hardcoded, not configurable.

**Fields to extract:**

| Field | Source on page |
|---|---|
| Kills | `<dt>Kills:</dt>` → adjacent `<dd>` |
| Deaths | `<dt>Deaths:</dt>` → adjacent `<dd>` |
| Accuracy | `<dt>Accuracy:</dt>` → adjacent `<dd>` (e.g. `"86%"`) |
| Playtime | text following "In-game for" label |
| Clan history | list of clan blocks, each with name, tag, join date |

**K/D ratio** is computed on the backend: `Math.Round(kills / (double)deaths, 2)`.

**HTTP request headers** (mimic browser to avoid 403):
```
User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36
Accept-Language: ru-RU,ru;q=0.9
Accept: text/html,application/xhtml+xml
```

**Error handling:**
- Nickname not found (site returns 404) → `TryGetDataAsync` returns `null`
- Site unavailable / 5xx → return `null`, log warning
- Parse failure (site changed markup) → catch exception, log error, return `null`
- Frontend shows "Данные временно недоступны" — does not break the ticket page

**Parsing library:** `HtmlAgilityPack` (NuGet, added to `Awake.Infrastructure`).

> **Note:** Exact XPath/CSS selectors must be validated against live HTML during implementation. If stalcrafthq.com blocks HttpClient despite browser headers, escalate to Playwright headless browser.

---

## Cache Strategy — Stale-While-Revalidate

- **Storage:** `ConcurrentDictionary<string, (PlayerProfile Profile, DateTime CachedAt)>` inside `PlayerDataAggregator` (singleton).
- **TTL:** 1 hour.
- **On request:**
  1. Cache hit and data is fresh (< 1h) → return immediately.
  2. Cache hit and data is stale (≥ 1h) → return stale data immediately AND trigger background `Task.Run` refresh (fire-and-forget).
  3. Cache miss → await scrape, cache result, return.
- No eviction policy needed — at most 35 entries, memory impact is negligible.

---

## Backend Architecture

### Domain — new value objects

```
src/Awake.Domain/ValueObjects/
  ClanEntry.cs      — record ClanEntry(string ClanName, string ClanTag, string Since)
  PlayerProfile.cs  — record PlayerProfile(int Kills, int Deaths, double KdRatio,
                          string Accuracy, string Playtime,
                          IReadOnlyList<ClanEntry> ClanHistory)
```

### Application — interface updates

**`src/Awake.Application/Common/Interfaces/IPlayerDataAggregator.cs`** — unchanged signature, but `PlayerDataResult` becomes typed.

**`src/Awake.Application/Common/Models/PlayerDataResult.cs`** — change `IReadOnlyList<object?> Sources` to `PlayerProfile? Profile`.

### Infrastructure — source + aggregator

**`src/Awake.Infrastructure/ExternalServices/PlayerData/IPlayerDataSource.cs`**
```csharp
Task<PlayerProfile?> TryGetDataAsync(string nickname, CancellationToken ct = default);
```
(was `object?`)

**`src/Awake.Infrastructure/ExternalServices/PlayerData/Sources/StubDataSource.cs`** — deleted (replaced by real source).

**`src/Awake.Infrastructure/ExternalServices/PlayerData/Sources/StalcraftHqDataSource.cs`** — new.
- Named `HttpClient` (`"stalcrafthq"`) injected via constructor.
- Fetches `https://stalcrafthq.com/characters/EU/{Uri.EscapeDataString(nickname)}`.
- Parses HTML with `HtmlAgilityPack.HtmlDocument`.
- Returns `PlayerProfile?`.

**`src/Awake.Infrastructure/ExternalServices/PlayerData/PlayerDataAggregator.cs`** — updated.
- Holds `ConcurrentDictionary` cache.
- Implements stale-while-revalidate logic.
- Iterates registered `IPlayerDataSource` implementations; returns first non-null result.

**`src/Awake.Infrastructure/DependencyInjection.cs`** — add:
```csharp
services.AddHttpClient("stalcrafthq", c => {
    c.BaseAddress = new Uri("https://stalcrafthq.com");
    c.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 ...");
    c.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9");
    c.Timeout = TimeSpan.FromSeconds(10);
});
services.AddSingleton<IPlayerDataAggregator, PlayerDataAggregator>();
services.AddTransient<IPlayerDataSource, StalcraftHqDataSource>();
```

### API — new controller

**`src/Awake.API/Controllers/PlayersController.cs`**
```
GET /api/players/{nickname}   [Authorize]
  → 200 PlayerProfile
  → 404 if aggregator returns null
```

---

## Frontend Architecture

### New type (`src/types/api.ts`)
```ts
export interface ClanEntry { clanName: string; clanTag: string; since: string }
export interface PlayerProfile {
  kills: number
  deaths: number
  kdRatio: number
  accuracy: string
  playtime: string
  clanHistory: ClanEntry[]
}
```

### New API client (`src/api/players.ts`)
```ts
export const playersApi = {
  getProfile: (nickname: string): Promise<PlayerProfile> =>
    apiClient.get<PlayerProfile>(`/players/${encodeURIComponent(nickname)}`),
}
```

### Ticket detail page (`src/routes/_auth.tickets.$ticketId.tsx`)

Replace the existing stub "Данные игрока" card with a TanStack Query-powered card:

```ts
const { data: profile, isLoading } = useQuery({
  queryKey: ['player-profile', ticket.gameNickname],
  queryFn: () => playersApi.getProfile(ticket.gameNickname),
  staleTime: 60 * 60 * 1000,   // 1 hour — matches backend cache TTL
  retry: false,
})
```

**UI layout:**
```
┌─ Данные игрока ──────────────────────────────┐
│  Убийства        121 559                      │
│  Смерти           48 879                      │
│  К/Д               2.49                       │
│  Точность            86%                      │
│  Время в игре    388 дней                     │
│                                               │
│  История кланов                               │
│  [HARD] Try Hard   3 мес. назад               │
│  [LOVE] Awake      ...                        │
└───────────────────────────────────────────────┘
```

- Loading state: skeleton rows (same height as content).
- Error / null: single line "Данные временно недоступны" in muted text.
- Numbers formatted with `toLocaleString('ru-RU')`.

---

## i18n keys (`ru.json`) — under `"profile"`

```json
"profile": {
  "title": "Данные игрока",
  "kills": "Убийства",
  "deaths": "Смерти",
  "kd": "К/Д",
  "accuracy": "Точность",
  "playtime": "Время в игре",
  "clanHistory": "История кланов",
  "unavailable": "Данные временно недоступны"
}
```

---

## Testing

- **Unit:** `PlayerDataAggregatorTests` — cache hit (fresh), cache hit (stale → background refresh triggered), cache miss, source returns null.
- **Unit:** `StalcraftHqDataSourceTests` — parse valid HTML fixture → correct `PlayerProfile`; parse HTML with missing fields → null; HTTP 404 → null.
- HTML fixtures stored as embedded `.html` files in the test project.
- No integration tests against the live site (fragile, CI-unfriendly).
