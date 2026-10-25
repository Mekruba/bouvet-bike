# BouvetBike

Web portal for a (fictitious) bike repair shop: customers place and follow maintenance orders, staff manage them in an admin view.

- `backend/`: ASP.NET Core 10 minimal API, one project in DDD modules (`Orders`, `Catalog`, `Discounts`, `Users`, `Shared`), EF Core + SQLite, ASP.NET Core Identity, xUnit
- `frontend/`: React 19 + TypeScript (Vite), React Router
- `docs/`: [requirements](docs/requirements.md), [architecture](docs/architecture.md), [AI workflow](docs/ai-workflow.md)

## Setup

### With Docker (quickest)

Needs only Docker with Compose.

```sh
cp .env.example .env           # set ADMIN_EMAIL and ADMIN_PASSWORD; SEED_SAMPLE_DATA=true for sample orders
docker compose up --build -d   # or: just up
```

Open http://localhost:8080 and log in at `/login` with the admin from `.env`. nginx serves the frontend and proxies `/api` to the backend. Data lives in the `data` volume; `docker compose down -v` stops the app and deletes it.

### For development

With Nix: `direnv allow` (or `nix develop`), then `just setup`. The flake provides .NET 10, Node 24, `just` and editor tooling. Without Nix: install the .NET 10 SDK and Node.js 22+, then `dotnet restore` in `backend/` and `npm install` in `frontend/`.

```sh
just admin admin@bouvetbike.local 'Choose-A-Passw0rd'   # the first admin, kept in user secrets
just dev                                               # backend :5080 + frontend :5173
```

| URL | What |
|---|---|
| http://localhost:5173 | Customer portal (`/admin` for staff) |
| http://localhost:5080/scalar/v1 | API docs |

- **Sample data:** `just dev-sample` fills a **new** database with about 30 orders (every status, history, notes, discounts, a price override, a full and a closed day). An existing database is never touched, so run `just reset-db` first. Orders for `kari.nordmann@example.com` can be found under **Find my order**.
- **Demo discount codes** in development: `WELCOME10` (10 %) and `SPRING100` (100 NOK off).
- **Database:** `backend/BouvetBike/bouvetbike.db`, created on start. There are no migrations, so run `just reset-db` after changing the data model, or after `just admin` if the database already exists.

## Configuration

Set in `appsettings.json`, user secrets or environment variables (`:` becomes `__`). For Docker, `.env` sets `ADMIN_EMAIL`, `ADMIN_PASSWORD`, `ASPNETCORE_ENVIRONMENT`, `PORT` (8080) and `SEED_SAMPLE_DATA`.

| Variable | Default | What it does |
|---|---|---|
| `Admin__Email`, `Admin__Password` | none | First admin, created on start if missing |
| `SeedSampleData` | `false` | Sample orders for a newly created database |
| `ConnectionStrings__Default` | `Data Source=bouvetbike.db` | SQLite file |
| `Capacity__MaxOrdersPerDay` | `5` | Starting default per weekday; admins change it in the staff area |
| `Capacity__SearchDays` | `60` | How far ahead to look for the next open date |
| `RateLimiting__OrderLookups__PermitsPerMinute` | `30` | Order lookups per IP per minute |
| `DataProtection__KeysPath` | user profile | Where login and confirmation keys are kept (`/data/keys` in Docker) |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` adds the demo codes and Scalar |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | unset | Trust a reverse proxy's `X-Forwarded-For` (on in Docker) |
| `API_PROXY_TARGET` (frontend) | `http://localhost:5080` | Backend the Vite dev server proxies to |

## Using the app

- **Book a service** (`/`): pick a service and a due date (each day shows the slots left), describe the bike, optionally add a discount code, confirm.
- **Find my order** (`/orders/find`): the booking reference opens that order; an email or phone lists every booking made with it. Notes can be changed until the repair starts.
- **Accounts** (optional, `/register`): confirm the email, then **My orders** lists your orders, including earlier guest bookings with that email.
- **Staff area** (`/admin`): orders with search, filters, capacity overview, status changes and internal notes. Admins also manage final prices, services, capacity, discount codes and staff.

## Test

```sh
just test
```

## Assumptions

The full list, with the reasoning, is in [docs/requirements.md](docs/requirements.md#interpretation-and-assumptions).

- **No account needed.** The reference alone opens an order; an email or phone lists that person's bookings. Anyone who knows them can see those bookings, as the assignment describes; rate limiting is the mitigation.
- **No email delivery.** The API returns the confirmation link a real email would contain.
- **One service per order**, with its name and price copied onto the order.
- **"Before the repair has started" means status `New`.**
- **Capacity:** 5 per day by default, set per weekday and per date by admins. Cancelled orders don't count; a full date gets the next available date.
- **The due date is when the bike is ready.** A service that takes several days can't be due sooner than that; drop-off isn't tracked.
- **Status lifecycle:** New → In progress / Waiting for customer → Completed, or Cancelled. Completed and Cancelled are final.
- **Refusals say why:** 400/422 wrong input, 403 wrong role, 405 unsupported method, 409 the order's state forbids it.

## Reflection

<!-- TODO -->
