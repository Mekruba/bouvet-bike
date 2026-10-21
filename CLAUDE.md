# BouvetBike

Interview assignment: bike repair shop with a customer portal and an admin page. Spec summary in `docs/requirements.md`.

## Commands (from repo root, inside `nix develop` / direnv)

- `just dev`: backend (:5080, dotnet watch) + frontend (:5173)
- `just test`: all tests (`dotnet test` in `backend/`, `npm test` in `frontend/`)
- `just reset-db`: delete the SQLite DB after model changes (no migrations; `EnsureCreated()`)
- Frontend lint/build: `npm run lint`, `npm run build` in `frontend/`

## Layout

- `backend/BouvetBike`: one project, **modules by business area**: `Orders/`, `Catalog/`, `Discounts/`, `Users/` (Identity: `ApplicationUser`, `Roles`), `Shared/`. Inside a module, only the layer folders it needs:
  - `Api/`: minimal API route groups as `Map<Feature>Endpoints(this RouteGroupBuilder)` extensions, registered under `/api` in `Program.cs`. Keep endpoints thin.
  - `Contracts/`: the module's public surface. Request/response records (DataAnnotations validation, max lengths from domain constants) and service interfaces (`ICustomerOrderService`, `IAdminOrderService`, `ICapacityService`, `ICatalogService`, `IDiscountService`, `IAccountService`; `ICurrentUser` in Shared). Services take request records and return response records. Required value types are nullable with `[Required]` (`Guid?`, `DateOnly?`) so a missing field is a validation error, not a default value.
  - `Application/`: implementations of the `Contracts/` interfaces, and their options. Private to the module. Gets the caller via `ICurrentUser`, never `HttpContext`.
  - `Domain/`: aggregates, value objects, rules. Throw `DomainException` on rule violations. **No EF Core, ASP.NET, Identity, or other `Api/Contracts/Application/Persistence` references.**
  - `Persistence/`: `IEntityTypeConfiguration<T>` per type (picked up automatically, no attributes on domain types) and seed data.
  - `Shared/`: `Domain/` (Entity, AggregateRoot, DomainException, Guard, Money, Email, PhoneNumber), `Contracts/` (`MoneyResponse`, `PagedResponse<T>`, `ICurrentUser`), `Application/` (`NotFoundException`), `Persistence/` (the single `AppDbContext`, converters, seeding), `Api/` (`ApiExceptionHandler`, health).
  - Module dependencies point one way: `Orders` → `Catalog`, `Discounts`. Other modules may use only a module's `Contracts` and `Domain`, never its `Application`, `Persistence` or `Api`. `Shared.Domain` depends on no module. All of this is enforced by `ArchitectureTests`; add new modules to its `Modules` list.
- `backend/BouvetBike.Tests`: xUnit, with folders mirroring the modules. Domain unit tests, `PersistenceTests` (EF round-trips), API integration tests via `ApiFactory` (WebApplicationFactory + temp SQLite file per fixture).
- `frontend/src`: `pages/` per route, `api/client.ts` fetch wrapper (`api<T>()`, throws `ApiError` carrying ProblemDetails).

## Conventions and gotchas

- Validation uses .NET 10 `AddValidation()` + DataAnnotations on DTOs. **Request types must be declared in their own files under a namespace.** Types declared in `Program.cs` are silently not validated.
- Errors are RFC 7807 ProblemDetails. Business rule violations: throw `DomainException` in the domain; `ApiExceptionHandler` turns it into a 400 with `detail` (plus any `Details` as extra fields); `NotFoundException` becomes a 404. Input validation failures: 400 with `errors`. The frontend `ApiError` reads both.
- Frontend calls relative `/api/...` paths; Vite proxies to the backend. Don't hardcode backend URLs.
- Keep docs in `docs/` current when making architectural decisions; log notable AI interactions in `docs/ai-workflow.md`.

## Domain conventions

- Private setters; create through static factories, change through methods that enforce the rules. Validate everything before mutating.
- Rule violation → `throw new DomainException("…")` (user-facing message).
- Time via injected `TimeProvider` (never `DateTime.Now`); timestamps are UTC `DateTime`. Tests use `FakeTimeProvider` from `TestData.Clock()`.
- Value objects: records with `private init` setters and a private parameterless constructor (EF Core needs it to materialize complex types).
- Keep the model simple: add an entity or table only when a requirement needs it. Prefer fields and value objects on the aggregate.
- New domain entity → add `IEntityTypeConfiguration<T>`; max lengths come from constants on the domain type.
- SQLite stores decimals as TEXT: don't `Sum`/`OrderBy` money in LINQ-to-SQL; compute in memory.
- No migrations: after model changes run `just reset-db`.
