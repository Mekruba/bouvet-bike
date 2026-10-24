# BouvetBike

Web portal for a (fictitious) bike repair shop: customers place and follow maintenance orders, staff manage them in an admin view.

- `backend/` — ASP.NET Core 10 minimal API in one project, organised in DDD modules by business area (`Orders/`, `Catalog/`, `Discounts/`, `Users/`, `Shared/`, each with `Api/`, `Contracts/`, `Application/`, `Domain/`, `Persistence/`), EF Core + SQLite, ASP.NET Core Identity, xUnit
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

Without `just`:

```sh
cd backend
dotnet user-secrets set --project BouvetBike "Admin:Email" "admin@bouvetbike.local"
dotnet user-secrets set --project BouvetBike "Admin:Password" "Choose-A-Passw0rd"
```

Then log in at http://localhost:5173/login. Under **Staff area → Staff**, the admin can add mechanics and other admins.

The SQLite database (`backend/BouvetBike/bouvetbike.db`) is created on first start. Daily capacity is set in the staff area (Capacity); `Capacity:MaxOrdersPerDay` in `appsettings.json` only sets the starting default for every weekday. After changing the data model, run `just reset-db`.

## Run with Docker

Needs only Docker with Compose.

```sh
cp .env.example .env   # set ADMIN_EMAIL and ADMIN_PASSWORD
docker compose up --build -d   # or: just up
```

The app runs at http://localhost:8080 (`PORT` in `.env` changes it). nginx serves the built frontend and proxies `/api` to the backend, which isn't published. The database and data protection keys are kept in the `data` volume. `docker compose down` stops the app (or `just down`); add `-v` to delete the data as well.

Production is the default environment, so there's no demo data and no Scalar. Set `ASPNETCORE_ENVIRONMENT=Development` in `.env` to get the demo discount codes.

## Using the app

**Customers** (no account needed):
- **Book a service** (`/`): choose a service and a due date (days show how many slots are left), describe the bike, optionally apply a discount code to see the price, then confirm.
- **Find my order** (`/orders/find`): **either** the booking reference (opens that order) **or** the email or phone used when booking (lists every booking made with it, any status, newest first). An order shows its status and price, and notes can be changed until the repair starts.
- With an account (`/register`, password typed twice): you confirm your email, then log in. Orders are linked to the account and listed under **My orders**, including earlier guest bookings made with the same email. There's no email delivery, so after registering you're taken straight to the confirmation page the email would have linked to.

**Staff** (`/admin`, log in first, see [Admin login](#admin-login)):
- **Orders:** today's counts and the next 14 days' capacity (click a day to filter), search, filters, sorting and paging. An order's page has details, status actions with history, internal notes, discount code, and (Admin) the final price and delete.
- **Services:** the catalog. Admins add, edit and retire services.
- **Capacity:** weekday defaults (e.g. Saturday 3, Sunday 0 = closed) and a 4-week calendar where admins give single dates more or less capacity, or close them; adjusted dates show against their default, e.g. "6/5".
- **Discount codes** and **Staff:** Admin only.

## Test

```sh
just test
```

## Assumptions

The full list, with the reasoning, is in [docs/requirements.md](docs/requirements.md#interpretation-and-assumptions). The main ones:

- **Customers don't need an account.** They find an order with the booking reference alone, or see all their bookings with the email or phone number they booked with. Accounts are optional; registering requires a confirmed email and links earlier guest bookings with that email.
- **Lookup by email or phone shows that person's bookings to anyone who knows them.** This follows the assignment's wording and is accepted on purpose, with rate limiting as the mitigation. A real product would send a one-time code.
- **No email delivery.** The confirmation link a real email would contain is returned by the API, and the frontend opens it directly.
- **Staff log in.** Mechanics manage orders; admins also manage prices, discount codes, capacity and staff.
- **One service per order.** Its name and price are copied onto the order, so later catalog changes don't alter it.
- **"Before the repair has started" means status `New`.** After that, customers can't change their notes.
- **Capacity:** default 5 per day, set per weekday by admins, with per-date exceptions (0 = closed). Cancelled orders don't count. A full date is rejected with the next available date.
- **Status lifecycle:** New → In progress / Waiting for customer → Completed, or Cancelled. Completed and cancelled orders are final.
- **A refused request says why:** 400/422 for wrong input, 403 for the wrong role, 405 for an unsupported method, 409 when the order's current state forbids it.

## Reflection

<!-- TODO -->
