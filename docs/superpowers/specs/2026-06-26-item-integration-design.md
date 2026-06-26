# Item Integration Design — Ticket Loadout

**Date:** 2026-06-26  
**Branch:** worktree-feature+stage-4  
**Status:** Approved

---

## Overview

Add a loadout (сборку) section to recruitment tickets. Players select up to 3 items from the official STALCRAFT item database (EXBO-Studio/stalzone-database): a sniper rifle (optional), a main weapon (required), and armor (required). Only items of rank Veteran, Master, or Legend are allowed.

The Appeal ticket type is removed from the UI entirely.

---

## Data Source

**Repository:** `EXBO-Studio/stalzone-database`  
**File:** `https://raw.githubusercontent.com/EXBO-Studio/stalzone-database/main/ru/listing.json`

Each entry in `listing.json`:
```json
{
  "data": "/items/weapon/sniper_rifle/1r79g.json",
  "icon": "/icons/weapon/sniper_rifle/1r79g.png",
  "name": { "lines": { "ru": "СКТ-40" } },
  "color": "RANK_MASTER",
  "status": { "state": "PERSONAL_ON_USE" }
}
```

**Item ID** is derived from the filename without extension (e.g. `1r79g`).

**Icon URL** = `https://raw.githubusercontent.com/EXBO-Studio/stalzone-database/main/ru` + `icon` field.

**Allowed colors (ranks):**

| Color value    | Display color |
|----------------|---------------|
| `RANK_VETERAN` | 🟣 Фиолетовый |
| `RANK_MASTER`  | 🔴 Красный    |
| `RANK_LEGEND`  | 🟡 Золотой    |

Items with `RANK_NEWBIE`, `RANK_STALKER`, or `DEFAULT` are excluded from search results.

---

## Item Slots

| Slot           | Category filter          | Required |
|----------------|--------------------------|----------|
| Снайперка      | `weapon/sniper_rifle`    | No (nullable) |
| Основное оружие| `weapon/*` (excl. `weapon/sniper_rifle`) | Yes |
| Броня          | `armor/*`                | Yes |

---

## Backend

### New: `ItemDto`
```csharp
// Awake.Application/Features/Items/Dtos/ItemDto.cs
public record ItemDto(string Id, string Category, string NameRu, string Icon, string Color);
```

### New: `IItemCacheService` + `ItemCacheService` (singleton)
```csharp
// Awake.Application/Common/Interfaces/IItemCacheService.cs
public interface IItemCacheService
{
    void Load(IEnumerable<ItemDto> items);
    IEnumerable<ItemDto> Search(string q, string? categoryPrefix);
}

// Awake.Infrastructure/ExternalServices/Items/ItemCacheService.cs
public class ItemCacheService : IItemCacheService
{
    private static readonly HashSet<string> AllowedColors =
        ["RANK_VETERAN", "RANK_MASTER", "RANK_LEGEND"];

    private Dictionary<string, ItemDto> _items = [];

    public void Load(IEnumerable<ItemDto> items) =>
        _items = items.ToDictionary(x => x.Id);

    public IEnumerable<ItemDto> Search(string q, string? categoryPrefix) =>
        _items.Values
            .Where(x => AllowedColors.Contains(x.Color))
            .Where(x => categoryPrefix == null || x.Category.StartsWith(categoryPrefix))
            .Where(x => x.NameRu.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Take(15);
}
```

### New: `ItemSyncHostedService`
- Implements `IHostedService`
- **On start:** fetches `listing.json`, parses items (category starts with `weapon/` or `armor/`), calls `IItemCacheService.Load`
- **Schedule:** calculates next Wednesday 00:00 UTC, sets a `Timer` to re-fetch every 7 days thereafter
- On fetch failure: logs error, keeps existing cache intact

### New: `ItemsController`
```
GET /api/items/search?q={query}&category={categoryPrefix}
```
- `q`: required, min 2 chars
- `category`: optional (e.g. `weapon/sniper_rifle`, `weapon`, `armor`)
- Returns: `ItemDto[]` (max 15)
- Auth: requires authenticated user

### Updated: `LoadoutSlotDto` + `LoadoutDto`
```csharp
// Awake.Application/Features/Tickets/Dtos/LoadoutSlotDto.cs
public record LoadoutSlotDto(string ItemId, string ItemName, string ItemIcon);

// Awake.Application/Features/Tickets/Dtos/LoadoutDto.cs
public record LoadoutDto(
    LoadoutSlotDto? Sniper,
    LoadoutSlotDto Weapon,
    LoadoutSlotDto Armor
);
```

### Updated: `Ticket` entity
```csharp
public LoadoutDto? Loadout { get; set; }
```
Stored as JSONB via EF Core value conversion (`System.Text.Json`).

### Updated: `TicketConfiguration`
```csharp
builder.Property(x => x.Loadout)
    .HasColumnType("jsonb")
    .HasConversion(
        v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
        v => JsonSerializer.Deserialize<LoadoutDto>(v, JsonSerializerOptions.Default));
```

### Updated: `CreateTicketCommand`
```csharp
public record CreateTicketCommand(
    string GameNickname,
    TicketType Type,
    string Description,
    LoadoutDto? Loadout
) : IRequest<Result<TicketListItemDto>>;
```

### Updated: `TicketDetailDto`
```csharp
public record TicketDetailDto(
    // ... existing fields ...
    LoadoutDto? Loadout
);
```

### Updated: Discord embed (`PostStatusUpdateAsync`)
When ticket type is `Recruitment` and `Loadout != null`, add embed fields:
```
Снайперка:        СКТ-40  (or "—" if null)
Основное оружие:  АК-74М
Броня:            Комбез «Страж»
```

### Migration
One new nullable JSONB column:
```sql
ALTER TABLE "Tickets" ADD COLUMN "Loadout" jsonb NULL;
```
Generated via `dotnet ef migrations add AddTicketLoadout`.

---

## Frontend

### Removed
- `TicketType.Appeal` card from `_auth.tickets.new.tsx`
- Type selector UI — type is hardcoded to `TicketType.Recruitment` on submit

### New: `ItemCombobox` component
**Props:**
```ts
interface ItemComboboxProps {
  categoryPrefix: string          // e.g. "weapon/sniper_rifle"
  excludeCategory?: string        // e.g. "weapon/sniper_rifle" (for main weapon slot)
  placeholder: string
  value: LoadoutSlot | null
  onChange: (item: LoadoutSlot | null) => void
  required?: boolean
}
```

**Behavior:**
- Input triggers search after 2+ chars with 300ms debounce
- Calls `GET /api/items/search?q=...&category=...`
- Dropdown shows: colored rank dot + item name
- On select: stores `{ itemId, itemName, itemIcon }` in parent state
- Has a clear button when item is selected
- Shows selected item name + colored dot when closed

**Rank color dots:**
```ts
const RANK_COLORS = {
  RANK_VETERAN: 'bg-purple-500',
  RANK_MASTER:  'bg-red-500',
  RANK_LEGEND:  'bg-yellow-400',
}
```

### Updated: `_auth.tickets.new.tsx`
New "Сборка" section after the Description field:
```
Снайперка (необязательно)
  <ItemCombobox categoryPrefix="weapon/sniper_rifle" />

Основное оружие *
  <ItemCombobox categoryPrefix="weapon" excludeCategory="weapon/sniper_rifle" />

Броня *
  <ItemCombobox categoryPrefix="armor" />
```

Submit payload adds:
```ts
loadout: {
  sniper: sniperSlot,   // null if not selected
  weapon: weaponSlot,
  armor:  armorSlot,
}
```

Validation: weapon and armor must be selected before submit.

### Updated: `_auth.tickets.$ticketId.tsx`
New "Сборка" card (visible to all users):
```
┌─────────────────────────┐
│ Сборка                  │
│                         │
│ Снайперка:   СКТ-40  🟣 │
│ Оружие:      АК-74М  🔴 │
│ Броня:       Комбез  🟡 │
└─────────────────────────┘
```
If `ticket.loadout` is null — card not shown.

### Updated: `types/api.ts`
```ts
export interface LoadoutSlot {
  itemId: string
  itemName: string
  itemIcon: string
}

export interface Loadout {
  sniper: LoadoutSlot | null
  weapon: LoadoutSlot
  armor: LoadoutSlot
}
```

### Updated: `ru.json`
```json
"loadout": {
  "title": "Сборка",
  "sniper": "Снайперка",
  "weapon": "Основное оружие",
  "armor": "Броня",
  "noSniper": "—",
  "sniperOptional": "Снайперка (необязательно)"
}
```

---

## Implementation Order

1. Backend: `ItemDto`, `IItemCacheService`, `ItemCacheService`, register as singleton
2. Backend: `ItemSyncHostedService`, register as hosted service
3. Backend: `ItemsController` with `/api/items/search`
4. Backend: `LoadoutSlotDto`, `LoadoutDto`
5. Backend: Update `Ticket` entity + `TicketConfiguration` (JSONB)
6. Backend: Migration `AddTicketLoadout`
7. Backend: Update `CreateTicketCommand` + handler + `TicketDetailDto`
8. Backend: Update Discord embed in `DiscordBotService`
9. Frontend: `types/api.ts` — add `LoadoutSlot`, `Loadout`
10. Frontend: `ru.json` — add loadout keys
11. Frontend: `ItemCombobox` component
12. Frontend: Update `_auth.tickets.new.tsx` — remove Appeal, add loadout slots
13. Frontend: Update `_auth.tickets.$ticketId.tsx` — show loadout card
