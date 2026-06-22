# Clan Platform — План разработки

## Описание проекта

Закрытая веб-платформа для управления кланом **Awake [LOVE]** (STALCRAFT).
Статистика, отчёты, управление отрядами, система тикетов с верификацией игроков.
Домен: **stalcraftclans.cc**

---

## Стек

| Слой | Технология |
|---|---|
| Frontend | **React 19 + Vite + TypeScript** |
| Роутинг | **TanStack Router** (type-safe, файловый) |
| Data Fetching | **TanStack Query** (кэш, loading/error, фоновые обновления) |
| UI | Tailwind CSS + shadcn/ui |
| Backend | **C# + ASP.NET Core 10 (Web API)** |
| Архитектура бэкенда | **Clean Architecture + CQRS (MediatR)** |
| ORM | Entity Framework Core |
| БД | PostgreSQL |
| Аутентификация | ASP.NET Identity + JWT |
| Валидация | FluentValidation (pipeline behavior MediatR) |
| Логирование | Serilog |
| Уведомления | Discord Webhook |
| Деплой | **Railway** (отдельные сервисы: frontend + backend + postgres) |
| Язык по умолчанию | Русский (английский — дополнительно, i18next) |
| Тема по умолчанию | Тёмная (светлая — по желанию пользователя) |

### Схема взаимодействия

```
[Next.js frontend] ──HTTP──▶ [ASP.NET Core API] ──▶ [PostgreSQL]
                                     │
                                     ├──▶ [Discord Webhook]
                                     └──▶ [Внешние сайты верификации]
```

### Структура проекта (Clean Architecture)

```
Awake.sln
├── src/
│   ├── Awake.Domain/          — Entities, Enums, Domain Exceptions
│   ├── Awake.Application/     — Use Cases, Commands, Queries, Interfaces, DTOs
│   ├── Awake.Infrastructure/  — EF Core, Repositories, External HTTP clients, Discord
│   └── Awake.API/             — Controllers, Middleware, DI-регистрация, Program.cs
└── tests/
    ├── Awake.Unit.Tests/
    └── Awake.Integration.Tests/
```

### Поток запроса (CQRS)

```
Controller
  └─▶ ISender.Send(new CreateTicketCommand(...))
        └─▶ CreateTicketCommandHandler : IRequestHandler
              ├─▶ FluentValidation (pipeline behavior)
              ├─▶ ITicketRepository
              └─▶ IDiscordNotifier
```

---

## Роли пользователей

Иерархия снизу вверх:

| Ранг | Права |
|---|---|
| `guest` | Просмотр своего профиля + отправка тикета |
| `member` | Просмотр статистики клана и своего отряда |
| `officer` | Рассмотрение тикетов, просмотр всех отрядов и статистики |
| `colonel` | Всё выше + управление составами всех отрядов, назначение лидеров |
| `leader` | Полный доступ: пользователи, ранги, отряды, тикеты, настройки |

> Новый пользователь после регистрации автоматически получает ранг `guest`.
> Повышение ранга — только вручную через панель `leader` или `colonel`.
> Регистрация — открытая (любой может создать аккаунт).

### Лидер отряда

`squad_leader` — **не ранг**, а флаг `IsLeader: bool` на записи `SquadMember`.
Лидер отряда остаётся `member` по рангу, но получает доступ к управлению своим отрядом.

---

## Модуль: Аутентификация

- Открытая регистрация: логин + пароль (email опционально)
- После регистрации → ранг `guest`
- JWT токены (access + refresh)
- ASP.NET Identity для хэширования паролей и управления пользователями
- Middleware авторизации на каждом защищённом эндпоинте
- Rate limiting на `/auth/register` и `/tickets`

---

## Модуль: Отряды (Squads)

- Всего **5 отрядов**, каждый имеет:
  - Название и номер (1–5)
  - **1 лидер** (флаг `IsLeader`)
  - **до 4 бойцов**
  - Итого: 5 человек максимум на отряд

### Управление отрядом (colonel+ / лидер своего отряда)
- Назначить/снять лидера
- Добавить/убрать бойца
- Переместить бойца между отрядами

### Схема БД

```
Squad {
  Id, Name, Number (1-5)
}

SquadMember {
  SquadId     → Squad
  UserId      → User
  IsLeader    bool      // true у одного участника на отряд
  JoinedAt    DateTime
}
// Ограничение: max 5 участников на отряд
```

---

## Модуль: Тикеты (Tickets)

### Типы тикетов
- `recruitment` — заявка на вступление в клан (основной)
- `appeal` — апелляция (кик / бан)
- *(расширяется при необходимости)*

### Подача тикета (guest+)
- Форма: игровой никнейм, тип тикета, описание
- После отправки → статус `pending`
- Discord Webhook уведомляет officer+ о новом тикете

### Рассмотрение тикета (officer+)
- Список тикетов с фильтрами (статус, дата, тип)
- При открытии тикета — **автоматический запрос данных об игроке с внешних сайтов**
- Статусы: `pending` → `in_review` → `approved` / `rejected`
- Внутренний комментарий (виден только officer+)
- Discord Webhook уведомляет заявителя о решении

### Внешние источники данных (верификация)
Список сайтов задаётся в `appsettings.json`. При открытии тикета API делает запросы и отдаёт агрегированную карточку игрока на фронт:

```
[ ] Сайт 1 — уточнить
[ ] Сайт 2 — уточнить
```

> URL сайтов будут добавлены отдельно.

---

## Модуль: Статистика

- Комплексная статистика по каждому игроку (агрегируется с внешних сайтов)
- Дашборд клана: составы отрядов, активность
- История изменений состава
- *(детальный состав страницы статистики — отдельный этап)*

---

## Модуль: Темы и локализация

- **Тёмная тема по умолчанию** (Static palette: #0e0e0f фон, #3ddc84 акцент)
- Светлая тема — переключается пользователем, сохраняется в localStorage
- **Русский язык по умолчанию**
- Английский — дополнительно (i18n через next-intl)

---

## Структура маршрутов (Next.js)

```
/                         — лендинг / вход
/auth/register            — регистрация
/auth/login               — вход
/dashboard                — главная (по рангу)
/squads                   — список отрядов
/squads/[id]              — страница отряда
/tickets                  — мои тикеты (guest) / все тикеты (officer+)
/tickets/new              — подать тикет
/tickets/[id]             — просмотр / рассмотрение тикета
/stats                    — статистика клана
/stats/[username]         — статистика игрока
/manage                   — панель управления (officer+)
/manage/users             — управление пользователями и рангами (colonel+)
/manage/squads            — управление отрядами (colonel+)
/settings                 — настройки аккаунта (тема, язык)
```

## ASP.NET Core API — эндпоинты

```
POST /api/auth/register
POST /api/auth/login
POST /api/auth/refresh

GET  /api/squads
GET  /api/squads/{id}
PUT  /api/squads/{id}/members       (colonel+)
PUT  /api/squads/{id}/leader        (colonel+)

GET  /api/tickets                   (officer+: все / guest: свои)
POST /api/tickets
GET  /api/tickets/{id}
PUT  /api/tickets/{id}/status       (officer+)
POST /api/tickets/{id}/comments     (officer+)

GET  /api/users                     (colonel+)
PUT  /api/users/{id}/rank           (colonel+)

GET  /api/stats/clan
GET  /api/stats/player/{nickname}   (агрегация с внешних сайтов)
```

---

## Стандарты кода

### SOLID — применение в проекте

| Принцип | Как соблюдается |
|---|---|
| **S** — Single Responsibility | Каждый Handler делает одно: один Command = одна операция |
| **O** — Open/Closed | Новые фичи = новые Command/Handler, без правки существующих |
| **L** — Liskov | Репозитории и сервисы за интерфейсами, реализации взаимозаменяемы |
| **I** — Interface Segregation | `ITicketRepository`, `ISquadRepository` — узкие интерфейсы, не один большой `IRepository` |
| **D** — Dependency Inversion | Все зависимости через конструктор, регистрируются в DI-контейнере |

### Dependency Injection

- Все сервисы, репозитории и клиенты регистрируются через `IServiceCollection`
- `Scoped` для репозиториев и DbContext, `Singleton` для конфигурации, `Transient` для лёгких утилит
- Никаких `new` внутри бизнес-логики — только через конструктор

### Читаемость кода

- Названия на **английском**: классы, методы, свойства — `CreateTicketCommand`, `TicketStatus.Pending`
- Комментарии на **русском** (если нужны) — для будущих разработчиков команды
- XML-документация на публичных интерфейсах и контроллерах
- Один файл = один класс
- Методы короткие: если метод не помещается на экран — выносим в private-метод или отдельный класс
- Магических строк нет: все константы в `static class Constants` или enum

### Обработка ошибок

- `Result<T>` паттерн в Application слое вместо исключений для ожидаемых ошибок
- Domain Exceptions для нарушений бизнес-правил
- Global Exception Middleware в API — перехватывает всё, логирует, возвращает стандартный `ProblemDetails`
- Никаких `catch (Exception e) {}` без логирования

### Pipeline Behaviors (MediatR)

```
Request
  → ValidationBehavior    (FluentValidation — автоматически для всех команд)
  → LoggingBehavior       (Serilog — логируем вход/выход каждой команды)
  → Handler
```

---

## Безопасность

- HTTPS (Railway обеспечивает автоматически)
- JWT с коротким TTL (access 15 мин, refresh 7 дней)
- Валидация всех входящих данных (FluentValidation на бэкенде, Zod на фронте)
- Rate limiting: `AspNetCoreRateLimit` на регистрацию и тикеты
- Авторизация через Policy-based authorization (ранги как claims)
- Внешние данные с сайтов — только чтение и отображение, без исполнения
- Логирование действий officer+ (Serilog → файл / Railway logs)
- Защита от CSRF (SameSite cookies для refresh token)
- CORS — только с домена stalcraftclans.cc

---

## Деплой (Railway)

```
railway/
  ├── frontend   (Next.js)        → stalcraftclans.cc
  ├── backend    (ASP.NET Core)   → api.stalcraftclans.cc
  └── postgres   (PostgreSQL)     → internal Railway network
```

- CI/CD через Railway GitHub integration
- Переменные окружения через Railway Variables
- Discord Webhook URL — в переменных окружения (не в коде)

---

## Этапы разработки

### Этап 1 — Фундамент
- [ ] Инициализация проекта: ASP.NET Core Web API + Next.js
- [ ] Схема БД + Entity Framework миграции (User, Squad, SquadMember, Ticket)
- [ ] ASP.NET Identity + JWT аутентификация
- [ ] Базовый фронт: вход, регистрация, layout с навигацией

### Этап 2 — Отряды
- [ ] API: CRUD отрядов, управление составом
- [ ] Фронт: список отрядов, страница отряда, панель colonel+

### Этап 3 — Тикеты
- [ ] API: создание, список, смена статуса, комментарии
- [ ] Фронт: форма подачи, панель рассмотрения
- [ ] Discord Webhook: уведомление о новом тикете и решении
- [ ] Интеграция с внешними сайтами: карточка игрока

### Этап 4 — Статистика
- [ ] Агрегация данных игрока с внешних сайтов
- [ ] Дашборд клана
- [ ] Страница статистики игрока

### Этап 5 — UX и локализация
- [ ] Тёмная / светлая тема (переключатель)
- [ ] Русский + английский (next-intl)
- [ ] Мобильная адаптация (responsive)

### Этап 6 — Безопасность и деплой
- [ ] Rate limiting + FluentValidation
- [ ] Serilog логирование
- [ ] Railway деплой (frontend + backend + postgres)
- [ ] Привязка домена stalcraftclans.cc
- [ ] HTTPS + CORS настройка

---

## Открытые вопросы

1. **Внешние сайты** — какие конкретно URL / API использовать для верификации игрока?
2. **Статистика** — какие именно поля показывать в карточке игрока (K/D, рейтинг, клан-история)?
3. **Отчёты отрядов** — свободный текст или структурированная форма?
