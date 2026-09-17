# Backend

Один процесс, один образ и один квартет проектов (ADR-0001):

- `Bugget.Domain` — доменные модели, лист графа: зависит только от BCL;
- `Bugget.Contracts` — провод наружу и такой же лист графа: только то, что сгенерировано
  из `specs/contracts/**/openapi.yaml`;
- `Bugget.Application` — сервисы, доменные события, порты (`I*DbClient` и прочие), а также
  команды и результаты, которыми сервисы разговаривают с транспортом; в контрактные типы
  их переводят мапперы в `Bugget.Api`;
- `Bugget.Infrastructure` — Postgres, файловое хранилище, внешние интеграции, миграции,
  Redis, фоновая очередь: реализует порты прикладного слоя;
- `Bugget.Api` — контроллеры, SignalR-хаб, middleware и DI-композиция.

Внутри `Bugget.Api`, `Bugget.Application` и `Bugget.Infrastructure` сохранены подпапки
`Users/` и `Authorization/`: это бывшие отдельные сервисы, у которых своя БД
(`users_db`) и свой набор внешних маршрутов. Отдельными проектами они больше не являются.

Внешние маршруты разведены по префиксам, которые снимает nginx:

| Внешний путь | Модуль |
| --- | --- |
| `/api/app/workspaces/{ws}/teams/{team}/...` | reports |
| `/api/users/...` | users |
| `/api/authorization/...` | authorization |
| `/_internal/auth` (субзапрос nginx) | authorization |

Модули не ходят друг к другу по HTTP: адаптеры в `backend/Bugget.Api/Modules/InProcess` подменяют
бывшие межсервисные вызовы прямыми.

## Команды

```sh
dotnet build Bugget.slnx
dotnet test Bugget.UnitTests/Bugget.UnitTests.csproj                 # unit
dotnet test Bugget.Architecture.Tests/Bugget.Architecture.Tests.csproj   # границы слоёв
dotnet test Bugget.IntegrationTests/Bugget.IntegrationTests.csproj   # нужен Docker (Testcontainers)
```

Тестовые проекты делятся по имени: `*.IntegrationTests` идут в медленный гейт, остальные —
в быстрый. Списки не ведутся: новый тестовый проект находится поиском и запускается сам.
