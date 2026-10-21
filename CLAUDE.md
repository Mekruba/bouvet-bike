# BouvetBike

Interview assignment: bike repair shop with a customer portal and an admin page. Spec summary in `docs/requirements.md`.

## Commands (from repo root, inside `nix develop` / direnv)

- `just dev`: backend (:5080, dotnet watch) + frontend (:5173)
- `just test`: all tests (`dotnet test` in `backend/`, `npm test` in `frontend/`)
- `just reset-db`: delete the SQLite DB after model changes (no migrations; `EnsureCreated()`)
- Frontend lint/build: `npm run lint`, `npm run build` in `frontend/`

## Layout

- `backend/BouvetBike`: one project, **modules by business area**: `Orders/`, `Catalog/`, `Discounts/`, `Users/` (Identity: `ApplicationUser`, `Roles`), `Shared/`. Inside a module, only the layer folders it needs:
  - `Api/`: `<Module>Module.cs` with `Add<Module>Module(this IServiceCollection)` (DI registrations) and `Map<Module>Endpoints(this RouteGroupBuilder)`, both called from `Program.cs`. Endpoints are private static handlers returning `TypedResults` (`Results<…>` unions); they only check HTTP-level things and call one service method. Name handlers after their resource (`GetServiceType`, not `GetById`): `WithName(nameof(...))` route names must be unique across the whole app, or startup fails.
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
- Rule violation → `throw new DomainException("…")` (user-facing message, 400/422). If the aggregate's *current state* is the reason, and another state would allow it, throw `StateConflictException` instead (409). A rule about *other* resources (capacity, discount code) stays a plain `DomainException`.
- Time via injected `TimeProvider` (never `DateTime.Now`); timestamps are UTC `DateTime`. Tests use `FakeTimeProvider` from `TestData.Clock()`.
- Value objects: records with `private init` setters and a private parameterless constructor (EF Core needs it to materialize complex types).
- Keep the model simple: add an entity or table only when a requirement needs it. Prefer fields and value objects on the aggregate.
- New domain entity → add `IEntityTypeConfiguration<T>`; max lengths come from constants on the domain type.
- SQLite stores decimals as TEXT: don't `Sum`/`OrderBy` money in LINQ-to-SQL; compute in memory.
- No migrations: after model changes run `just reset-db`.

## API conventions (REST + RFC 6902)

- Plural kebab-case resources, no verbs in URLs. `POST` → 201 + `Location`; `PATCH` → 200 + resource; `DELETE` → 204; unsupported method → 405.
- Updates are `PATCH` with JSON Patch (`application/json-patch+json`, `Microsoft.AspNetCore.JsonPatch.SystemTextJson`). Apply the patch to a `…Patch` model in `Contracts/`, validate it, then call the domain. Never patch an entity directly. Endpoint: `.Accepts<JsonPatchDocument<TPatch>>(JsonPatch.MediaType)` (gives 415) and `JsonPatch.FindMalformedOperation` (400). Service: build the model from the entity, `patch.ApplyAndValidate(model)` (409/422), then call domain methods, turning a `DomainException` into `UnprocessableException`.
- PATCH errors per RFC 5789: 400 malformed, 415 wrong media type, 409 failed `test` op (`ConflictException`), 422 bad path or invalid result (`UnprocessableException` with field errors). The full table is in `docs/architecture.md`.

## Testing (test-driven)

- Write failing **API tests** first (`HttpClient` against `ApiFactory`, real SQLite), commit them red, then implement. Domain unit tests cover rules on the aggregates. No mocks of services or the DbContext.
- `factory.CreateClient(Roles.Admin)` signs in as a role via `TestAuthHandler` (header `X-Test-Role`); `CreateClient(role: null)` is anonymous. Requests *without* the header use the real cookie sign-in, so `CreateClient(role: null)` plus `POST /api/session` or `POST /api/users` tests the real flow (each client has its own cookie jar). The factory seeds an admin: `ApiFactory.SeededAdminEmail`/`SeededAdminPassword`. `TestUsers` has the fixed user IDs. The test users exist in the database (seeded by `ApiFactory`).
- The app clock is `factory.Clock` (a `FakeTimeProvider`, today = `TestData.Today` = 2026-03-02); never depend on the real date. `factory.WithDbAsync(db => …)` sets up data that has no API yet.
- `ApiAssert` helpers: `PatchJsonAsync`, `AssertStatus`, `AssertProblem` (checks `application/problem+json`), `AssertFieldError`, `ReadAs<T>`, and `AssertSameJson` for responses containing lists (record equality compares lists by reference).
- Tests in a class share one database, so create the data you change (unique names) and don't assume exact counts. For orders, use `OrderTestHelpers.FreshDay()` for the due date unless the test is about capacity; more than 5 orders on one date make the next one fail as fully booked.
- An interface method without tests yet throws `NotImplementedException("Not yet test-driven.")`. Write its tests before implementing it.
