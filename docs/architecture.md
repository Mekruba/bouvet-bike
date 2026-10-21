# Architecture

## Overview

```
Browser ──▶ Vite dev server (:5173) ──/api/*──▶ ASP.NET Core API (:5080) ──▶ SQLite
            React SPA                            Endpoints → Domain
                                                 Infrastructure (EF Core)
```

## Backend layers

One project (`backend/BouvetBike`), with DDD layers as folders and namespaces:

```
Endpoints ──▶ Domain ◀── Infrastructure
    │                         ▲
    └────── AppDbContext ─────┘
```

- **Domain**: aggregates, value objects and their invariants. No framework dependencies.
- **Infrastructure**: persistence (`AppDbContext`, EF mapping via `IEntityTypeConfiguration<T>`).
- **Endpoints**: HTTP concerns (DTOs, validation), following the pattern load → domain call → save.

A separate Application layer (use-case services) is deliberately left out for now. Add `Application/` if endpoints start containing orchestration logic.

## Choices so far

| Area | Choice | Trade-off |
|---|---|---|
| Structure | Single project, DDD layers as folders | Domain logic stays isolated and unit-testable without multi-project ceremony. Boundaries are enforced by an architecture test (NetArchTest) instead of project references |
| API style | Minimal APIs under `/api`, one `Map…Endpoints` extension per feature | Less ceremony than controllers; endpoints stay thin and delegate to the domain |
| Business rules | Domain throws `DomainException`, which `DomainExceptionHandler` turns into a 400 ProblemDetails | One consistent path for rule violations; the domain stays unaware of HTTP |
| Persistence | EF Core + SQLite (file); mapping in `Infrastructure/` | Zero setup, real SQL, and domain types carry no EF attributes. Not suited to concurrent multi-instance use |
| Schema | `EnsureCreated()` on startup, no migrations | Fast iteration, but schema changes require deleting the DB. Switch to `dotnet ef migrations` if data must survive changes |
| Validation | .NET 10 built-in minimal API validation (`AddValidation()` + DataAnnotations) | No extra library; errors come back as RFC 7807 `ValidationProblemDetails` |
| Errors | `AddProblemDetails()` + `UseExceptionHandler()` | Consistent JSON error shape that the frontend `ApiError` understands |
| API docs | Built-in OpenAPI + Scalar UI | Generated from code, so it stays in sync |
| Frontend | React + TypeScript via Vite, React Router | Customer (`/`) and admin (`/admin`) as separate routes |
| Dev wiring | Vite proxy for `/api` | Same origin in dev, so no CORS config |
| Tests | xUnit + `WebApplicationFactory` against a temp SQLite file; NetArchTest; Vitest + Testing Library | Integration tests exercise the real HTTP pipeline and database |

## Customer/admin separation

<!-- TODO -->

## Domain model

<!-- TODO -->

## API

See Scalar at http://localhost:5080/scalar/v1 when running.
