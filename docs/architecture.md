# Architecture

## Overview

```
Browser ──▶ Vite dev server (:5173) ──/api/*──▶ ASP.NET Core API (:5080) ──▶ SQLite
            React SPA                            modules: Orders, Catalog, Discounts, Users
```

With Docker Compose, nginx takes the Vite server's place:

```
Browser ──▶ frontend: nginx (:8080) ──/api/*──▶ backend: ASP.NET Core (:8080, not published) ──▶ SQLite in the `data` volume
            built SPA, SPA fallback
```

## Backend structure

One project (`backend/BouvetBike`), split into **modules by business area**. Each module uses the same layer folders, but only the ones it needs:

| Folder | Contains |
|---|---|
| `Api/` | Minimal API endpoints (`Map…Endpoints` extensions) |
| `Contracts/` | **The module's public surface:** request/response DTOs (with DataAnnotations validation) and the service interfaces. The API and other modules depend only on this |
| `Application/` | **Implementations** of the `Contracts/` interfaces, plus their options. Use cases that coordinate more than one thing (e.g. place order = check capacity + redeem discount + save). Private to the module |
| `Domain/` | Aggregates, value objects and business rules. No framework dependencies |
| `Persistence/` | EF Core mapping (`IEntityTypeConfiguration<T>`) and seed data |

```
BouvetBike/
├── Orders/      Api Contracts Application Domain Persistence
│                orders, status history, price override, capacity (weekday schedule, date overrides), guest lookup
├── Catalog/     Api Contracts Application Domain Persistence      service types and prices
├── Discounts/   Api Contracts Application Domain Persistence      discount codes
├── Users/       Api Contracts Application Persistence             accounts, sessions, email confirmation, staff
│                + ApplicationUser, Roles, Policies at the root (Identity, so no Domain/)
├── Shared/      Api Contracts Application Domain Persistence
│                error handling, health; ICurrentUser, paging; exceptions; Entity, Money, Email, PhoneNumber;
│                AppDbContext, conventions, seeding
└── Program.cs   composition root: registers and maps every module
```

**Dependency rules** (enforced by `ArchitectureTests`):
- `*.Domain` never depends on EF Core, ASP.NET, Identity or any `Api`/`Contracts`/`Application`/`Persistence` folder.
- `*.Contracts` never depends on EF Core, ASP.NET or any `Api`/`Application`/`Persistence` folder. Contracts may use domain enums and constants.
- `*.Application` never depends on any `Api` folder or on `Microsoft.AspNetCore.Http`; it reaches the caller through `ICurrentUser`.
- **A module may use another module's `Contracts` and `Domain`, never its `Application`, `Persistence` or `Api`.** For example, Orders calls `Catalog.Contracts.ICatalogService`; it never sees how the catalog is implemented. `Shared` is the host and is exempt: `AppDbContext` and seeding wire the modules together.
- `Shared.Domain` depends on no module.
- Dependencies between modules point one way: `Orders` may use `Catalog` and `Discounts`, never the reverse. An order is placed *with* a `ServiceType` and *with* an `AppliedDiscount`; the catalog and discounts know nothing about orders.
- **When Users needs to tell Orders something** (a customer registered, so link their guest orders), it calls an interface in its own contracts, `ICustomerAccountEvents`, which Orders implements. Users never references Orders (an architecture test checks this), so the one-way dependency holds.
- There is one `AppDbContext` (in `Shared/Persistence`). It picks up every module's mapping, so modules share a transaction without extra plumbing.

## Choices so far

| Area | Choice | Trade-off |
|---|---|---|
| Structure | One project, modules by business area, layer folders inside each | Code for a feature lives together, and the domain stays isolated and unit-testable without multi-project ceremony. Boundaries are enforced by architecture tests (NetArchTest) instead of project references |
| API style | Minimal APIs under `/api`, one `Map…Endpoints` extension per feature | Less ceremony than controllers; endpoints stay thin and delegate to the domain |
| Business rules | Domain throws `DomainException`, which `ApiExceptionHandler` turns into a 400 ProblemDetails | One consistent path for rule violations; the domain stays unaware of HTTP |
| Persistence | EF Core + SQLite (file); one `AppDbContext`, mapping per module | Zero setup, real SQL, and domain types carry no EF attributes. Not suited to concurrent multi-instance use |
| Schema | `EnsureCreated()` on startup, no migrations | Fast iteration, but schema changes require deleting the DB. Switch to `dotnet ef migrations` if data must survive changes |
| Validation | .NET 10 built-in minimal API validation (`AddValidation()` + DataAnnotations) | No extra library; errors come back as RFC 7807 `ValidationProblemDetails` |
| Errors | `AddProblemDetails()` + `UseExceptionHandler()` | Consistent JSON error shape that the frontend `ApiError` understands |
| API docs | Built-in OpenAPI + Scalar UI, with the `///` comments included (`GenerateDocumentationFile`) | Generated from code, so it stays in sync. Handlers are `internal` because the generator ignores comments on private methods; `OpenApiTests` checks the comments arrive |
| Frontend | React + TypeScript via Vite, React Router; no UI or data library | Customer portal at `/`, staff area at `/admin`. Small, typed API layer and a handful of reusable components instead of a component library: fewer dependencies, full control over the look. See [Frontend](#frontend) |
| Dev wiring | Vite proxy for `/api` | Same origin in dev, so no CORS config |
| Containers | Two images (backend on `aspnet`, frontend on unprivileged nginx), both non-root; nginx proxies `/api` like the Vite proxy does; the database and data protection keys sit in one volume | Same origin, so still no CORS. Only nginx is published. The backend trusts `X-Forwarded-For` so rate limiting sees client IPs, which is safe only because nothing else can reach it. Keys on the volume keep logins and confirmation links valid across restarts; they're stored unencrypted, which is fine for a single-host volume |
| Tests | xUnit + `WebApplicationFactory` against a temp SQLite file; NetArchTest; Vitest + Testing Library | Integration tests exercise the real HTTP pipeline and database |

## Customer/admin separation

- **ASP.NET Core Identity** with cookie authentication, behind our own `/api/auth` endpoints (register, login, logout, me). `MapIdentityApi` was considered and rejected: it has no logout, its register can't take name or phone, and it doesn't assign roles. The SPA and API share an origin through the Vite proxy, so cookies work with no token handling in the frontend.
- **Roles:** `Admin`, `Mechanic`, `Customer`. Staff endpoints live under `/api/admin` and require a staff role; customer endpoints under `/api` allow anonymous (guest) access.
- **The roles and the first admin are seeded on startup** (`IdentitySeeder`). The admin comes from configuration (`Admin:Email`, `Admin:Password`) in user secrets (`just admin <email> <password>`) or environment variables, never committed. Without it, the app starts with a warning and no admin.
- Customers never need an account; see [requirements](requirements.md#customers-and-accounts).
- Passwords: at least 8 characters with Identity's default complexity rules. 5 wrong passwords lock the account for 15 minutes. The cookie is `HttpOnly` and `SameSite=Strict`, which also protects the cookie-authenticated API against cross-site request forgery in current browsers.

## Domain model

### Identity vs. domain

`ApplicationUser : IdentityUser<Guid>` (in `Users/`) holds login data plus `Name`; email and phone come from Identity. **The domain never references it.** Domain types store user IDs as plain `Guid`s (`CustomerId`, `ChangedById`, `OverriddenById`), which keeps every `Domain/` folder free of Identity.

### Aggregates

Five aggregates, plus Identity's users. Everything else is a value or a field on the order.

| Model | Module | Key fields |
|---|---|---|
| **MaintenanceOrder** | Orders | Reference, Contact, CustomerId?, BikeBrand, BikeModel?, ServiceTypeId + ServiceName + ServicePrice (snapshot), DueDate, Status, CustomerNotes?, InternalNotes?, Discount?, PriceOverride?, CreatedAt, UpdatedAt |
| ↳ OrderStatusChange | Orders | From, To, ChangedById?, ChangedAt, Reason?: the audit trail of the lifecycle |
| **ServiceType** | Catalog | Name, Description?, BasePrice, EstimatedDuration, IsActive |
| **DiscountCode** | Discounts | Code (unique), Type, Value, ValidFrom?, ValidTo?, MaxUses?, TimesUsed, IsActive |
| **CapacitySchedule** | Orders | Monday … Sunday: default capacity per weekday (one schedule) |
| **CapacityOverride** | Orders | Date (unique), MaxOrders (0 = closed), Reason? |

**Order pricing:** `DiscountAmount` and `Total` are computed. The total is the price override if set, otherwise the service price minus the discount.

**Value objects:**
- `Shared`: `Money` (amount + currency), `Email`, `PhoneNumber` (E.164)
- `Orders`: `ContactInfo` (Name, Email, Phone), `OrderReference`, `PriceOverride` (Amount, Reason, OverriddenById, OverriddenAt)
- `Discounts`: `AppliedDiscount` (DiscountCodeId, Code, Type, Value)

**Enums:** `OrderStatus` (New, InProgress, WaitingForCustomer, Completed, Cancelled), `DiscountType` (Percentage, FixedAmount).

```
MaintenanceOrder ── OrderStatusChange*        ServiceType        DiscountCode
CapacitySchedule (one: weekday defaults)      CapacityOverride* (one per date)

MaintenanceOrder ··▶ ServiceType, User?   (by ID, with foreign keys)
```

### Rules and where they live

| Rule | Owner |
|---|---|
| Valid status transitions; every change recorded in the history | `MaintenanceOrder` |
| Customer notes editable only while `New`; internal notes at any status, never shown to customers | `MaintenanceOrder` |
| Only active services can be ordered; the service's name and price are copied onto the order, and choosing the same service again keeps the original price | `MaintenanceOrder` |
| Due date not in the past (only checked when set or changed) | `MaintenanceOrder` |
| Completed and cancelled orders are read-only, except that the price can be overridden on completed orders | `MaintenanceOrder` |
| Total = override ?? service price − discount; the discount never exceeds the price; one discount per order | `MaintenanceOrder`, `AppliedDiscount` |
| Discount code validity window and usage limit | `DiscountCode` |
| A date takes at most its capacity (override, else weekday default) in non-cancelled orders; suggest the next open date | `ICapacityService` (Orders): it spans many orders, so no single aggregate can enforce it |

### Status lifecycle

| From | Allowed to |
|---|---|
| New | InProgress, WaitingForCustomer, Cancelled |
| InProgress | WaitingForCustomer, Completed, Cancelled |
| WaitingForCustomer | InProgress, Cancelled |
| Completed | (none) |
| Cancelled | (none) |

Defined in `OrderStatusTransitions`; anything else throws `DomainException`. `WaitingForCustomer` is reachable from `New` (e.g. a question before work starts).

### Decisions

- **Contact is a snapshot on the order, not a link to the user.** Guests must be able to order, and the order should record who to contact for this repair.
- **Kept simple, on purpose:** bike brand is a string, there is one service per order, and notes are two text fields (customer and internal). Separate tables for brands, saved bikes, order lines, note history and per-date capacity were modelled first and then cut: none of them is needed by a requirement. Each can be added back without changing the rest of the order.
- **The status history stays.** The lifecycle is one of the business rules, and the history shows who changed what and when.
- **The discount is stored as the code's terms, not as an amount.** The order keeps a snapshot of type and value, so the discount is recalculated when the service changes and isn't affected by later edits to the code.
- **Rejected: a passwordless "shadow" user per guest order.** If a guest orders with an email that already has an account, that design either attaches orders to someone else's account or needs email verification to merge. That's complexity with no requirement behind it.
- **Rejected: a separate Customer entity.** Registered customers are Users with the `Customer` role; guests are covered by the contact snapshot.

## Domain code conventions

- **Encapsulation:** private setters, static factories (`Place`, `Create`) and intention-revealing methods (`ChangeStatus`, `UpdateCustomerNotes`). A rule violation throws `DomainException`, which becomes a 400.
- **Methods validate before mutating,** so a failed call never leaves an aggregate half-changed.
- **Time** comes from an injected `TimeProvider` (tests use `FakeTimeProvider`). Timestamps are UTC `DateTime`; "today" for due dates is the server's local date.
- **IDs** are UUIDv7, created in the domain (`Entity`), so an entity has its identity before it's saved.
- **Max lengths** are constants on the domain types, shared by the domain checks and the EF configuration.

### Persistence mapping

- Value objects with several fields (`ContactInfo`, `Money`, `AppliedDiscount`, `PriceOverride`) are EF Core complex types, stored as columns on the owner's table. Single-value ones (`Email`, `PhoneNumber`, `OrderReference`) are stored as strings through value converters.
- Enums are stored as strings, which keeps the database readable.
- **Timestamps are UTC everywhere.** A convention marks every `DateTime` read from the database as UTC: SQLite doesn't store `DateTime.Kind`, and an unmarked value would be sent without its `Z`, so clients would read it as local time.
- The status history is its own table, cascade-deleted with the order and auto-included when it's loaded.
- **SQLite caveats:** decimals are stored as TEXT, so money can't be summed or sorted in SQL (totals are computed in memory). `DateTimeOffset` can't be ordered in SQL, which is why timestamps are UTC `DateTime`.

## Frontend

```
frontend/src/
├── api/         types.ts (mirrors the backend contracts), client.ts (fetch, ProblemDetails → ApiError),
│                endpoints.ts (one typed function per endpoint), useApi.ts (load, reload, replace)
├── auth/        AuthProvider (current user from GET /api/users/me), useAuth, RequireRole (route guard)
├── components/  Button, Card, Field (TextField, SelectField, TextAreaField, Checkbox), Alert, DataTable,
│                Pagination, Dialog / ConfirmDialog, StatusBadge, CapacityStrip, PriceSummary, OrderSummaryTable, Layout
├── lib/         format.ts (money, dates, labels), useForm.ts (form state + API errors), jsonPatch.ts (diffPatch),
│                orderAccess.ts (the last order search, in sessionStorage)
├── pages/       customer pages; pages/admin/ for the staff area
└── styles/      tokens.css, base.css, layout.css, components.css: the only CSS, imported once
```

- **Styling is centralized.** Colours, type, spacing and shape are CSS variables in `tokens.css` (light mode only, colours from the Bouvet logo). Components use class names from `components.css`; there are no per-component stylesheets or inline styles, apart from the capacity meter's computed width.
- **Reuse:** every form is built from the same `Field` components and the `useForm` hook. That hook binds inputs and turns a 400/422 response into errors next to the right fields. Every list is a `DataTable` (columns, optional sorting, clickable rows, loading and empty states). The `CapacityStrip` is both the customer's date picker and the staff capacity overview.
- **Shared order table:** `OrderSummaryTable` is used by both "My orders" and the guest lookup results.
- **Edits send JSON Patch with only what changed.** `diffPatch` compares the original and edited values and produces `replace`/`remove` operations, matching the API's RFC 6902 contract and its "apply only what changed" rule.
- **Guest access:** the reference in the URL (`/orders/BB-…`) is all an order page needs. Searching by email or phone sends it in the `Order-Contact` header, never in the URL; the last search is kept in `sessionStorage` (this tab only), so going back from an order shows the list again.
- **Auth:** the session is the HttpOnly cookie; the frontend only asks who is logged in. `RequireRole` hides pages the user can't use, but the API enforces every rule itself.
- **Staff filters live in the URL** (`?status=New&sort=CustomerName`), so the back button, reloads and shared links keep them.
- **Tests:** two smoke tests (the app renders; the staff area sends anonymous users to login). Behaviour is covered by the backend API tests; the UI was checked by scripted browser runs with screenshots.

## API

Contract-first: request/response records live in each module's `Contracts/`, together with the use-case interfaces; implementations live in `Application/`. Endpoints map a route to one service method and nothing else. Live docs: Scalar at http://localhost:5080/scalar/v1.

### REST conventions

- **Resources are plural, kebab-case nouns** (`/api/service-types`, `/api/orders`). No verbs in URLs.
- `GET` → 200 · `POST` (create) → **201 with a `Location` header** and the created resource · `PATCH` → 200 with the updated resource · `DELETE` → 204. A method a resource doesn't support → 405.
- **Updates are `PATCH` with a JSON Patch document (RFC 6902)**, `Content-Type: application/json-patch+json`. The patch is applied to a *patch model* (e.g. `ServiceTypePatch`) whose camelCase properties are the patch paths. The model is then validated and passed to the domain, so a patch can never bypass a business rule. Patches are atomic: if any operation fails, nothing is saved.
- **`PATCH` status codes follow RFC 5789 §2.2:** 400 malformed patch document · 415 wrong content type · **409 a `test` operation failed** (use `test` for optimistic concurrency) or the result conflicts with another resource · **422 unknown or read-only path, or the patched resource would be invalid**, with field errors.
- 401 when not logged in, 403 when logged in with the wrong role.
- **Responses with personal data are sent with `Cache-Control: no-store`** (RFC 9111): `/api/orders`, `/api/admin/orders`, `/api/users`, `/api/session`, `/api/admin/staff` and `/api/email-confirmation-requests`, through `WithNoStore()` on the route group. `GET /api/orders` answers differently depending on the `Order-Contact` header or the login cookie, so a cached copy could reach the wrong person. Catalog, capacity and price quotes are public and left cacheable.

### Endpoints

**Catalog** (implemented test-first, see `CatalogApiTests`):

| Route | Access | Service method |
|---|---|---|
| `GET /api/service-types` | anyone | `ICatalogService.GetAllAsync` (active only, sorted by name) |
| `GET /api/service-types?includeInactive=true` | staff | `GetAllAsync` |
| `GET /api/service-types/{id}` | anyone | `GetByIdAsync` |
| `POST /api/service-types` | Admin | `CreateAsync` → 201; 409 on a duplicate name (case-insensitive) |
| `PATCH /api/service-types/{id}` | Admin | `PatchAsync`; paths `/name`, `/description`, `/price`, `/estimatedMinutes`, `/isActive` |
| `DELETE /api/service-types/{id}` | | 405: orders reference service types, so retire one with `PATCH /isActive false` |

**Discount codes** (Admin; test-first: `DiscountCodeApiTests`):

| Route | Access | Service method |
|---|---|---|
| `GET /api/admin/discount-codes` | Admin | `IDiscountService.GetAllAsync`: all codes, with how often each was used |
| `GET /api/admin/discount-codes/{id}` | Admin | `GetByIdAsync` |
| `POST /api/admin/discount-codes` | Admin | `CreateAsync` → 201; existing code (any case) → 409 |
| `PATCH /api/admin/discount-codes/{id}` | Admin | `PatchAsync`; paths `/type`, `/value`, `/validFrom`, `/validTo`, `/maxUses`, `/isActive`. `/code` is read-only: orders keep the code they used |
| `DELETE /api/admin/discount-codes/{id}` | | 405: retire a code with `PATCH /isActive false` |

**Orders, customer side** (implemented test-first: `CapacityApiTests`, `PriceQuoteApiTests`, `PlaceOrderApiTests`, `CustomerOrderApiTests`):

| Route | Access | Service method |
|---|---|---|
| `GET /api/capacity?from=&days=` | anyone | `ICapacityService.GetAsync`: bookings per day, default today + 14 days, max 60 |
| `GET /api/price-quotes?serviceTypeId=&discountCode=` | anyone | `ICustomerOrderService.QuoteAsync`: never uses up the code |
| `POST /api/orders` | anyone | `PlaceAsync` → 201, `Location: /api/orders/{reference}`; full date → 400 with `nextAvailableDate` |
| `GET /api/orders` + `Order-Contact: <email or phone>` | anyone (rate-limited) | `FindByContactAsync`: every order with that contact, newest first |
| `GET /api/orders` | Customer | `GetMyOrdersAsync`: orders linked to the account |
| `GET /api/orders/{reference}` | anyone with the reference (rate-limited) | `GetAsync` |
| `PATCH /api/orders/{reference}` | anyone with the reference (rate-limited) | `PatchAsync`: JSON Patch, `/notes` only, while New |

**Orders, staff side** (implemented test-first: `AdminOrderQueryApiTests`, `AdminOrderUpdateApiTests`, `AdminOrderLifecycleApiTests`):

| Route | Access | Service method |
|---|---|---|
| `GET /api/admin/orders?search=&status=&serviceTypeId=&dueFrom=&dueTo=&sort=&page=` | staff | `IAdminOrderService.SearchAsync` |
| `GET /api/admin/orders/{id}` | staff | `GetAsync` |
| `PATCH /api/admin/orders/{id}` | staff | `PatchAsync`: details, service, due date, `/internalNotes`, `/discountCode` |
| `DELETE /api/admin/orders/{id}` | Admin | `DeleteAsync` → 204 |
| `GET` · `POST /api/admin/orders/{id}/status-changes` | staff | `GetStatusChangesAsync`, `AddStatusChangeAsync` → 201 + `Location`; invalid transition for the current status → 409 |
| `GET /api/admin/orders/{id}/status-changes/{changeId}` | staff | `GetStatusChangeAsync` |
| `GET /api/admin/orders/{id}/price-override` | staff | `GetPriceOverrideAsync`; 404 when none is set |
| `PUT` · `DELETE /api/admin/orders/{id}/price-override` | Admin | `SetPriceOverrideAsync` → **201 when created, 200 when replaced** (RFC 9110); `ClearPriceOverrideAsync` → 204, also when none is set (idempotent) |

**Capacity settings** (test-first: `CapacitySettingsApiTests`):

| Route | Access | Service method |
|---|---|---|
| `GET` · `PATCH /api/admin/capacity/schedule` | view: staff · change: Admin | `GetScheduleAsync`, `PatchScheduleAsync`: weekday defaults, JSON Patch on `/monday` … `/sunday` |
| `GET /api/admin/capacity/overrides?from=&to=` | staff | `GetOverridesAsync` |
| `PUT /api/admin/capacity/overrides/{date}` | Admin | `SetOverrideAsync` → 201 when created, 200 when replaced; the date (`yyyy-MM-dd`) is the natural key |
| `DELETE /api/admin/capacity/overrides/{date}` | Admin | `RemoveOverrideAsync` → 204, idempotent |

`GET /api/capacity` returns each day's `capacity` and its weekday `defaultCapacity`, so clients can show deviations ("6/5") and closed days.

**Users** (test-first: `AccountApiTests`, `StaffApiTests`):

| Route | Access | Service method |
|---|---|---|
| `POST /api/users` | anyone | `IAccountService.RegisterAsync`: a Customer account with an unconfirmed email → 201, `Location: /api/users/me`, with `emailConfirmationLink` (stand-in for email); not signed in; taken email → 409 |
| `PUT /api/users/{id}/email-confirmation` | anyone with the token | `ConfirmEmailAsync` → 204 (idempotent, so `PUT` to a singleton rather than `POST`); bad token → 400. Links the customer's guest orders |
| `POST /api/email-confirmation-requests` | anyone | `RequestEmailConfirmationAsync` → 202, with a new link only for an unconfirmed account |
| `GET /api/users/me` | logged in | `GetCurrentAsync` |
| `POST /api/session` | anyone | `LoginAsync` → 200 with the user and a cookie; wrong email or password → 401 (same answer for both); right password but unconfirmed email → 403 |
| `DELETE /api/session` | anyone | `LogoutAsync` → 204 |
| `GET` · `POST /api/admin/staff`, `GET /api/admin/staff/{id}` | Admin | `GetStaffAsync`, `CreateStaffAsync` → 201, `GetStaffMemberAsync` |

The session is a resource without its own URI (it lives in the cookie), so logging in is a `POST` answered with 200 and the signed-in user, which RFC 9110 allows for a `POST` whose response describes the result. Every 401 carries a `WWW-Authenticate` challenge (RFC 9110 requires one).

"Staff" means Admin or Mechanic. Deleting orders and overriding prices are Admin-only.

**Why these shapes:**
- **The reference is the key to one order; the email or phone finds the references.** The email or phone goes in an `Order-Contact` request header, not a query parameter or a `POST /lookup`: the order list stays a plain `GET`, and personal data stays out of URLs, browser history and logs. No `X-` prefix (RFC 6648). See [requirements](requirements.md#order-lookup) for the trade-off, and the rate limit that goes with it.
- **A quote is a `GET`**: it's safe and idempotent, a calculation, not a new resource.
- **Status changes are a sub-collection (`POST …/status-changes`)**, not a patched field: each change is a history entry with its own author, time and reason.
- **The price override is a singleton sub-resource (`PUT`/`DELETE`)**: Admin-only, so it can't share the staff `PATCH`.
- **`/api/admin/…` is the staff view** of the same orders: the routing-based separation the assignment asks for.
- In a `POST`, a broken business rule is 400 and in a `PATCH` 422 (RFC 5789), except when the order's current state is what forbids it: then 409 (see Errors).
- **Admin search** matches the reference (exact, any case, prefix optional), email (exact, ignoring case), phone (exact, any format), and customer name or bike brand (substring, ignoring case). Email and phone are value objects stored as whole values, so a partial match would need extra search columns; exact matching keeps the model simple and covers how staff look people up.
- **A staff `PATCH` applies only what it changes**: internal notes can still be edited on a completed order, while details can't. Moving the due date re-checks capacity (the order doesn't count against itself); a full date is 422 with `nextAvailableDate`.

### Errors

All errors are RFC 7807 ProblemDetails.

**When a request is well-formed but refused, the status says *why*,** so the client knows what to do next:

| Why | Status | Client should |
|---|---|---|
| What you sent breaks a rule, whatever the state (past due date, 150 % discount, invalid phone) | **400** (`POST`/`PUT`), **422** (`PATCH`, RFC 5789) | change the input |
| Who you are: your role may not do this | **403** | ask someone who may |
| What you're doing: this resource never supports that method (`DELETE` on a service type) | **405** | use another operation |
| When you're doing it: *this* resource's current state forbids it, another state would allow it (`New → Completed`, editing a completed order, notes after the repair started) | **409** | reload the resource |

A rule that depends on *other* resources (a fully booked due date, a used-up discount code) is a 400: reloading the order wouldn't help, and the response says what to change instead (`nextAvailableDate`). In the domain, state rules throw `StateConflictException`, a `DomainException` subtype, so the domain stays HTTP-agnostic and only says *what kind* of rule was broken.

| Status | When | Extra fields |
|---|---|---|
| 400 | Request validation failed (DataAnnotations on the request record, or `RequestValidationException`) | `errors`: field → messages |
| 400 | Business rule violated (`DomainException`) | anything in `DomainException.Details`, e.g. `nextAvailableDate` when a due date is full |
| 401 / 403 | Not logged in / wrong role | 401 carries `WWW-Authenticate` |
| 404 | `NotFoundException`, including an order the caller may not see | |
| 405 | The resource doesn't support the method | |
| 429 | Too many order lookups from one client | `Retry-After` header |
| 409 | `StateConflictException`: the resource's current state forbids the operation (in `POST` and `PATCH` alike) | |
| 409 | `ConflictException`: duplicate, or a failed JSON Patch `test` | |
| 415 | Wrong `Content-Type` (e.g. `application/json` for a JSON Patch) | |
| 422 | `UnprocessableException`: a JSON Patch that can't be applied, or that would leave the resource invalid | `errors`: field → messages |

Enums are sent as strings (`"InProgress"`), dates as `yyyy-MM-dd`.
