# BouvetBike

Interview assignment: bike repair shop with a customer portal and an admin page. Spec summary in `docs/requirements.md`.

## Commands (from repo root, inside `nix develop` / direnv)

- `just dev`: backend (:5080, dotnet watch) + frontend (:5173)
- `just test`: all tests (`dotnet test` in `backend/`, `npm test` in `frontend/`)
- `just reset-db`: delete the SQLite DB after model changes (no migrations; `EnsureCreated()`)
- Frontend lint/build: `npm run lint`, `npm run build` in `frontend/`

## Layout

- `backend/BouvetBike`: one project, DDD layers as folders/namespaces:
  - `Domain/`: entities, value objects, enums, business rules. Throw `DomainException` on rule violations. **No EF Core, ASP.NET, or Infrastructure/Endpoints references** (enforced by `ArchitectureTests`).
  - `Infrastructure/`: `AppDbContext`. EF mapping goes in `IEntityTypeConfiguration<T>` classes here (picked up automatically), not attributes on domain types.
  - `Endpoints/`: minimal API route groups as `Map<Feature>Endpoints(this RouteGroupBuilder)` extensions, registered under `/api` in `Program.cs`. Request/response DTOs live here. Keep endpoints thin: load aggregate → call domain method → save.
- `backend/BouvetBike.Tests`: xUnit. Domain unit tests plus API integration tests via `ApiFactory` (WebApplicationFactory + temp SQLite file per fixture).
- `frontend/src`: `pages/` per route, `api/client.ts` fetch wrapper (`api<T>()`, throws `ApiError` carrying ProblemDetails).

## Conventions and gotchas

- Validation uses .NET 10 `AddValidation()` + DataAnnotations on DTOs. **Request types must be declared in their own files under a namespace.** Types declared in `Program.cs` are silently not validated.
- Errors are RFC 7807 ProblemDetails. Business rule violations: throw `DomainException` in the domain; `DomainExceptionHandler` turns it into a 400 with `detail`. Input validation failures: 400 with `errors`. The frontend `ApiError` reads both.
- Frontend calls relative `/api/...` paths; Vite proxies to the backend. Don't hardcode backend URLs.
- Keep docs in `docs/` current when making architectural decisions; log notable AI interactions in `docs/ai-workflow.md`.
