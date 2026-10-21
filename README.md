# BouvetBike

Web portal for a (fictitious) bike repair shop: customers place and follow maintenance orders, staff manage them in an admin view.

- `backend/` — ASP.NET Core 10 minimal API in a single project with DDD folders (`Domain/`, `Infrastructure/`, `Endpoints/`), EF Core + SQLite, xUnit
- `frontend/` — React 19 + TypeScript (Vite), React Router, Vitest
- `docs/` — [requirements](docs/requirements.md), [architecture](docs/architecture.md), [AI workflow](docs/ai-workflow.md)

## Setup

### With Nix (recommended)

```sh
direnv allow        # or: nix develop
just setup
```

The flake provides the .NET 10 SDK, Node 24, `just`, and editor tooling (OmniSharp, vtsls, netcoredbg, prettierd).

### Without Nix

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Node.js 22+, then:

```sh
cd backend && dotnet restore
cd ../frontend && npm install
```

## Run

```sh
just dev
```

or in two terminals:

```sh
cd backend && dotnet watch --project BouvetBike
cd frontend && npm run dev
```

| URL | What |
|---|---|
| http://localhost:5173 | Customer portal |
| http://localhost:5173/admin | Admin page |
| http://localhost:5080/scalar/v1 | API docs (Scalar, dev only) |
| http://localhost:5080/openapi/v1.json | OpenAPI document |

The Vite dev server proxies `/api/*` to the backend on port 5080.

### Demo data

In Development (`just dev`), two discount codes are seeded for trying the price quote and discount features: `WELCOME10` (10 %) and `SPRING100` (100 NOK off, 50 uses). Other environments, including the tests, get no demo data.

### Admin login

Staff pages need a login. The first admin is created on startup from configuration, which is kept in [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) rather than in the repo:

```sh
just admin admin@bouvetbike.local 'Choose-A-Passw0rd'   # at least 8 characters, with upper and lower case, a digit and a symbol
just reset-db                                          # only if the database already exists
```

Then log in with `POST /api/session`. The admin can create mechanics and other admins with `POST /api/admin/staff`.

The SQLite database (`backend/BouvetBike/bouvetbike.db`) is created on first start. After changing the data model, run `just reset-db`.

## Test

```sh
just test
```

## Assumptions

<!-- TODO -->

## Reflection

<!-- TODO -->
