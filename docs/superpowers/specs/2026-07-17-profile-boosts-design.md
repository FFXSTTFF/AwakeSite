# Profile Boosts — Design Spec

**Date:** 2026-07-17
**Scope:** игрок отмечает в профиле нужные ему бусты (бафы) на КВ; отметки видны
всем участникам клана в трёх местах. Выдача офицером, сбросы и этапы КВ —
**вне скоупа** (осознанное решение пользователя: «пока что не будем ничего
делать» после отметки игроком).

## Problem

Перед клановой войной офицерам нужно знать, кому какие бафы готовить. Сейчас
это устно/в Discord. Нужен способ: игрок один раз отмечает в профиле нужные
типы бустов, остальные участники (Member+) видят отметки.

Типы бустов фиксированы в коде — ровно 4: Усиление, Кратковременное усиление,
Скорость, Защита.

## Domain + Persistence

Новая сущность `PlayerBoostRequest` (`Awake.Domain/Entities/`):

```csharp
public class PlayerBoostRequest
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public BoostType BoostType { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

Новый enum `BoostType` (`Awake.Domain/Enums/`):

```csharp
public enum BoostType
{
    Damage = 0,       // Усиление
    ShortDamage = 1,  // Кратковременное усиление
    Speed = 2,        // Скорость
    Defense = 3,      // Защита
}
```

EF-конфигурация по образцу `PlayerBuildProofConfiguration`: таблица
`PlayerBoostRequests`, unique index `(UserId, BoostType)`, FK на Users с
`DeleteBehavior.Cascade`. Одна миграция `AddPlayerBoostRequests`.

Репозиторий `IPlayerBoostRequestRepository` (по образцу
`IPlayerBuildProofRepository`):

- `GetByUserIdAsync(Guid userId, CancellationToken ct)`
- `GetByUserIdsAsync(IReadOnlyList<Guid> userIds, CancellationToken ct)` — для
  обогащения отрядов
- `GetAllAsync(CancellationToken ct)` — для сводки; с `Include(r => r.User)`,
  чтобы handler сводки не делал второй запрос за никами
- `ReplaceForUserAsync(Guid userId, IReadOnlyList<BoostType> types, CancellationToken ct)`
  — удаляет старые записи пользователя и вставляет новые **одним
  SaveChangesAsync** (тот же принцип атомарности, что `MoveMemberAsync`)

## Application (CQRS)

- `SetMyBoostsCommand(Guid UserId, IReadOnlyList<BoostType> BoostTypes)` —
  полная замена набора (идемпотентно; тумблер на фронте шлёт новый полный
  список, никаких гонок add/remove). Handler: дедуп входного списка →
  `ReplaceForUserAsync`. Validator: каждый элемент `IsInEnum()`, размер списка
  ≤ 4.
- `GetMyBoostsQuery(Guid UserId)` → `IReadOnlyList<BoostType>`.
- `GetBoostsSummaryQuery()` → `IReadOnlyList<BoostSummaryEntryDto>`:

```csharp
public record BoostSummaryEntryDto(
    Guid UserId,
    string Username,
    string? GameNickname,
    IReadOnlyList<BoostType> BoostTypes);
```

  Только пользователи с ≥1 отметкой. Сортировка: по количеству отметок убыв.,
  затем по нику (`GameNickname ?? Username`, OrdinalIgnoreCase).

## API

Новый `BoostsController`, паттерн ответов как в `InventoryController`:

- `GET /api/profile/boosts` → `BoostType[]` — свои отметки. Просто
  `[Authorize]` без ранг-гейта — как остальные `api/profile/*` в
  `InventoryController` («свой … — любой ранг»).
- `PUT /api/profile/boosts`, body `record SetBoostsRequest(IReadOnlyList<BoostType> BoostTypes)`
  → 204. `[Authorize]`, без ранг-гейта.
- `GET /api/boosts/summary` → `BoostSummaryEntryDto[]`.
  `[RankAuthorize(UserRank.Member)]` — как `api/players/{userId}/inventory`
  (смотреть могут все участники, не только офицеры).

Enum на проводе — числа (в проекте нет `JsonStringEnumConverter`), т.е.
`GET /api/profile/boosts` отдаёт `[0, 2]`.

Обогащение существующих DTO:

- `SquadMemberDto` (+`IReadOnlyList<BoostType> BoostTypes`) — заполняется в
  `SquadMemberEnricher.ComputeAsync` (добавить `GetByUserIdsAsync` рядом с
  inventories/proofs — тот же batch-подход, без N+1; тип результата словаря
  расширяется до `(Flags, Kd, BoostTypes)`).
- `PlayerProfileDto` (+`IReadOnlyList<BoostType> Boosts`) — заполняется в
  `GetPlayerProfileQueryHandler` через `GetByUserIdAsync`.

## Frontend

Типы: `BoostType` — **не TS enum** (`erasableSyntaxOnly`), а const-объект +
union, как существующие enum-подобные типы в `types/api.ts`. Подписи (ru):
Damage «Усиление», ShortDamage «Кратк. усиление» (полная форма
«Кратковременное усиление» — только в своём профиле, где есть место), Speed
«Скорость», Defense «Защита». Все строки через i18n (`boosts.*`).

API-клиент `boostsApi`: `getMy()`, `setMy(types)`, `summary()`.
Query keys: `['boosts','my']`, `['boosts','summary']`; мутация `setMy`
инвалидирует оба + `['squads']` (попап) + профильные ключи.

### 1. Свой профиль (`_auth.profile.tsx`)

Карточка «Нужные бусты» рядом с инвентарём/пруфами: 4 чипа-тумблера.
Неактивный — `border-border text-muted-foreground`; активный —
`border-accent/30 bg-accent/10 text-accent` (стиль флагов билдов). Клик =
optimistic update (чип переключается сразу, `PUT` с новым полным набором,
откат при ошибке). Подпись: «Отметьте бафы, которые вам нужны на КВ — их
увидят офицеры и участники клана».

### 2. Публичный профиль (`_auth.players.$userId.tsx`)

Та же карточка read-only: только активные чипы, из `PlayerProfileDto.boosts`.
Если отметок нет — секция скрыта целиком. Общий компонент `BoostChips`
с пропом `readonly`; редактируемость = «смотрю свой профиль», ранг не при чём.

### 3. Попап на карточках отрядов (`MemberHoverInfo`)

Третий блок под флагами билдов: **только активные** чипы (в попапе паритет
«есть/нет» не нужен — 4 серых чипа были бы шумом). Нет отметок — блока нет,
высота попапа не меняется. Данные из `SquadMemberDto.boostTypes` через пропы,
новых запросов нет. Клэмп/флип попапа не трогается.

### 4. Сводная страница (`/boosts`, новый роут `_auth.boosts.tsx`)

Пункт «Бусты» в навигации (сайдбар + мобильный таб-бар), Member+.

- Десктоп: таблица в стиле лидерборда. Строки — только игроки с ≥1 отметкой;
  ник (`gameNickname ?? username`) — ссылка на `/players/$userId`. 4 колонки
  типов: акцентная галочка или прочерк. В шапке колонки — счётчик отметивших.
- Мобильный: карточки (ник + чипы активных бустов), как мобильный лидерборд.
- Пустое состояние: «Пока никто не отметил нужные бусты» + подсказка про
  профиль.
- Страница read-only для всех.

## Out of scope

- Отметка «выдано» офицером, кнопки сброса, этапы КВ, история выдач — всё
  отложено. Схема расширяема: nullable `IssuedAt`/`IssuedById` добавляются
  позже одной миграцией без ломки.
- Уведомления (Discord/колокольчик) о новых заявках.
- Привязка к OCR клановых войн (#83).
