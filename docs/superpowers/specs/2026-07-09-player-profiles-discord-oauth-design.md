# Профили игроков + Discord OAuth — дизайн

Дата: 2026-07-09
Статус: утверждён пользователем

## Проблема

Игрок подаёт заявку через Discord-бота **до** регистрации на сайте. Данные заявки
(игровой ник, статистика, экипировка) сохраняются «висячими» — тикет имеет
`DiscordUserId`, но `AuthorId = null`. Нужно: после появления аккаунта на сайте
автоматически связать эти данные и показать их на странице профиля.

## Решение (утверждённые требования)

1. **Аутентификация — только Discord OAuth.** Вход по логину/паролю удаляется.
   Связывание аккаунта с заявками — автоматическое по `DiscordUserId`.
2. **Профиль показывает:** игровую статистику, экипировку (Loadout) из заявки,
   отряд и ранг. Discord-инфо — только аватар и ник в шапке.
3. **Статистика хранится снапшотом в PostgreSQL** (переживает рестарты,
   профиль открывается мгновенно), с кнопкой принудительного обновления.
4. **Доступ:** свой профиль — любой залогиненный; чужие — member+.

## Архитектура

### Модель данных

`User` — добавляются поля:

```csharp
public string? DiscordUserId { get; set; }    // unique index — ключ связывания
public string? DiscordUsername { get; set; }
public string? DiscordAvatarUrl { get; set; }
```

- `PasswordHash` остаётся в схеме, но не используется (не ломаем миграции).
- `Username` заполняется из Discord `global_name` при первом входе.
- `GameNickname` подтягивается из последней Discord-заявки при связывании.

`PlayerStatsSnapshot` — новая таблица:

```csharp
public class PlayerStatsSnapshot : BaseEntity
{
    public string GameNickname { get; set; }  // unique index
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public double KdRatio { get; set; }
    public string Accuracy { get; set; }
    public string Playtime { get; set; }
    public List<ClanEntry> ClanHistory { get; set; }  // jsonb
    public DateTime FetchedAt { get; set; }
}
```

Ключ — **игровой ник, а не UserId**: снапшот создаётся при Discord-заявке,
когда аккаунта ещё нет. Данные не «висячие» — они лежат под ником и ждут.

`PlayerDataAggregator` — write-through: каждый успешный запрос статистики
(создание заявки, refresh профиля) сохраняет снапшот в БД. In-memory кэш
(12 ч TTL) остаётся первым уровнем.

### Аутентификация (Discord OAuth, authorization code flow на бэкенде)

```
Фронт: кнопка «Войти через Discord»
  → GET /api/auth/discord/login        (302 на discord.com/oauth2/authorize, scope=identify, state)
  → юзер подтверждает в Discord
  → GET /api/auth/discord/callback?code=...&state=...
      1. проверка state (CSRF)
      2. обмен code → access_token (client secret только на сервере)
      3. GET users/@me → id, username, global_name, avatar
      4. User по DiscordUserId: нашли → логин; не нашли → создали (Rank=Guest)
      5. связывание: тикеты с этим DiscordUserId и AuthorId=null
         → AuthorId=user.Id; GameNickname последней заявки → user.GameNickname
      6. выдача наших JWT (access+refresh, существующий ITokenService)
      7. redirect на фронт /auth/callback#token=...
```

- Эндпоинты `POST /api/auth/register` и `POST /api/auth/login` (пароль) — удаляются.
  `POST /api/auth/refresh` остаётся.
- Новые переменные окружения: `Discord__ClientSecret`, `Discord__OAuthRedirectUri`
  (ClientId уже есть — `Discord__ApplicationId`).
- Связывание идемпотентно: обновляются только тикеты с `AuthorId = null`,
  повторный вход ничего не дублирует.

### API профиля

```
GET  /api/players/me                — свой профиль (любой залогиненный)
GET  /api/players/{userId}          — чужой профиль (member+)
POST /api/players/me/stats/refresh  — принудительное обновление (202 Accepted, фон)
```

DTO ответа (общий для обоих GET):

```json
{
  "userId": "...", "username": "...",
  "discordUsername": "...", "discordAvatarUrl": "...",
  "rank": "Member", "gameNickname": "OopsITry",
  "squad": { "id": "...", "name": "...", "number": 2, "isLeader": false },
  "stats": {
    "kills": 121599, "deaths": 48901, "kdRatio": 2.49,
    "accuracy": "46%", "playtime": "454 days",
    "clanHistory": [{ "clanName": "Awake", "clanTag": "LOVE" }],
    "fetchedAt": "2026-07-09T15:11:50Z"
  },
  "loadout": { "sniper": {}, "weapon": {}, "armor": {} }
}
```

- `stats` читается из `PlayerStatsSnapshot` по `gameNickname` — без FlareSolverr.
- `squad`, `stats`, `loadout` — nullable.
- Refresh: возвращает `202` сразу, обновление в фоне (FlareSolverr 15–30 c),
  фронт поллит `GET /api/players/me`. Rate limit: 1 раз в 10 минут на ник.

### Фронтенд (TanStack Router)

```
/login            — одна кнопка «Войти через Discord» (/register удаляется)
/auth/callback    — принимает токен из redirect, сохраняет, → /dashboard
/profile          — свой профиль
/players/$userId  — чужой профиль (member+; guest → 403 → редирект на /profile)
```

Страница профиля (палитра Static #3ddc84, тёмная тема): шапка с аватаром
Discord + ник + ранг + отряд; карточки статистики (убийства, смерти, K/D,
точность, время в игре); блок экипировки; история кланов; метка
«обновлено N ч. назад» + кнопка обновления.

## Обработка ошибок

- Discord вернул ошибку / юзер отменил OAuth → redirect `/login?error=discord`,
  тост «Не удалось войти через Discord».
- Снапшота нет и refresh не дал данных (оба источника null) → `stats: null`,
  фронт показывает «данные временно недоступны».
- Невалидный/просроченный `state` в callback → 400, redirect на `/login?error=discord`.

## Тестирование

- Unit: link-on-login хендлер — связывает висячие тикеты, не трогает чужие
  (`AuthorId != null`), идемпотентен при повторном входе.
- Unit: маппинг Profile DTO (null squad/stats/loadout).
- Unit: авторизация — guest получает 403 на чужой профиль.
- Unit: write-through агрегатора — успешный fetch сохраняет снапшот.
- Существующие тесты парольной auth — удаляются вместе с эндпоинтами.
- Ручная проверка: Discord-заявка до регистрации → вход через Discord →
  профиль сразу показывает ник, статистику и экипировку из заявки.

## Вне скоупа

- История снапшотов / графики прогресса (можно добавить позже — схема позволяет).
- Привязка Discord к существующим парольным аккаунтам (парольных аккаунтов не будет).
- Публичные профили без логина.
