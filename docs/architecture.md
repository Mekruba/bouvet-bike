# Architecture

## Overview

```
Browser ──▶ Vite dev server (:5173) ──/api/*──▶ ASP.NET Core API (:5080) ──▶ SQLite
            React SPA                            modules: Orders, Catalog, Discounts, Users
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
├── Orders/      Domain/ Persistence/            MaintenanceOrder and its status history
├── Catalog/     Domain/ Persistence/            ServiceType (services and prices)
├── Discounts/   Domain/ Persistence/            DiscountCode
├── Users/                                       ApplicationUser, Roles (Identity, so no Domain/)
├── Shared/      Domain/ Persistence/ Api/       Entity, Money, Email, PhoneNumber, DomainException;
│                                                AppDbContext, seeding; error handling, health
└── Program.cs
```

Folders are added as features are built (`Api/` and most of `Application/` come with the endpoints).

**Dependency rules** (enforced by `ArchitectureTests`):
- `*.Domain` never depends on EF Core, ASP.NET, Identity or any `Api`/`Contracts`/`Application`/`Persistence` folder.
- `*.Contracts` never depends on EF Core, ASP.NET or any `Api`/`Application`/`Persistence` folder. Contracts may use domain enums and constants.
- `*.Application` never depends on any `Api` folder or on `Microsoft.AspNetCore.Http`; it reaches the caller through `ICurrentUser`.
- **A module may use another module's `Contracts` and `Domain`, never its `Application`, `Persistence` or `Api`.** For example, Orders calls `Catalog.Contracts.ICatalogService`; it never sees how the catalog is implemented. `Shared` is the host and is exempt: `AppDbContext` and seeding wire the modules together.
- `Shared.Domain` depends on no module.
- Dependencies between modules point one way: `Orders` may use `Catalog` and `Discounts`, never the reverse. An order is placed *with* a `ServiceType` and *with* an `AppliedDiscount`; the catalog and discounts know nothing about orders.
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
| API docs | Built-in OpenAPI + Scalar UI | Generated from code, so it stays in sync |
| Frontend | React + TypeScript via Vite, React Router | Customer (`/`) and admin (`/admin`) as separate routes |
| Dev wiring | Vite proxy for `/api` | Same origin in dev, so no CORS config |
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

Three aggregates, plus Identity's users. Everything else is a value or a field on the order.

| Model | Module | Key fields |
|---|---|---|
| **MaintenanceOrder** | Orders | Reference, Contact, CustomerId?, BikeBrand, BikeModel?, ServiceTypeId + ServiceName + ServicePrice (snapshot), DueDate, Status, CustomerNotes?, InternalNotes?, Discount?, PriceOverride?, CreatedAt, UpdatedAt |
| ↳ OrderStatusChange | Orders | From, To, ChangedById?, ChangedAt, Reason?: the audit trail of the lifecycle |
| **ServiceType** | Catalog | Name, Description?, BasePrice, EstimatedDuration, IsActive |
| **DiscountCode** | Discounts | Code (unique), Type, Value, ValidFrom?, ValidTo?, MaxUses?, TimesUsed, IsActive |

**Order pricing:** `DiscountAmount` and `Total` are computed. The total is the price override if set, otherwise the service price minus the discount.

**Value objects:**
- `Shared`: `Money` (amount + currency), `Email`, `PhoneNumber` (E.164)
- `Orders`: `ContactInfo` (Name, Email, Phone), `OrderReference`, `PriceOverride` (Amount, Reason, OverriddenById, OverriddenAt)
- `Discounts`: `AppliedDiscount` (DiscountCodeId, Code, Type, Value)

**Enums:** `OrderStatus` (New, InProgress, WaitingForCustomer, Completed, Cancelled), `DiscountType` (Percentage, FixedAmount).

```
MaintenanceOrder ── OrderStatusChange*        ServiceType        DiscountCode

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
| Max 5 orders per due date, suggest the next free date | `ICapacityService` (Orders): it spans many orders, so no single aggregate can enforce it |

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

## API

Contract-first: request/response records live in each module's `Contracts/`, together with the use-case interfaces; implementations live in `Application/`. Endpoints map a route to one service method and nothing else. Live docs: Scalar at http://localhost:5080/scalar/v1.

### REST conventions

- **Resources are plural, kebab-case nouns** (`/api/service-types`, `/api/orders`). No verbs in URLs.
- `GET` → 200 · `POST` (create) → **201 with a `Location` header** and the created resource · `PATCH` → 200 with the updated resource · `DELETE` → 204. A method a resource doesn't support → 405.
- **Updates are `PATCH` with a JSON Patch document (RFC 6902)**, `Content-Type: application/json-patch+json`. The patch is applied to a *patch model* (e.g. `ServiceTypePatch`) whose camelCase properties are the patch paths. The model is then validated and passed to the domain, so a patch can never bypass a business rule. Patches are atomic: if any operation fails, nothing is saved.
- **`PATCH` status codes follow RFC 5789 §2.2:** 400 malformed patch document · 415 wrong content type · **409 a `test` operation failed** (use `test` for optimistic concurrency) or the result conflicts with another resource · **422 unknown or read-only path, or the patched resource would be invalid**, with field errors.
- 401 when not logged in, 403 when logged in with the wrong role.

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

**Orders, customer side** (implemented test-first: `CapacityApiTests`, `PriceQuoteApiTests`, `PlaceOrderApiTests`, `CustomerOrderApiTests`):

| Route | Access | Service method |
|---|---|---|
| `GET /api/capacity?from=&days=` | anyone | `ICapacityService.GetAsync`: bookings per day, default today + 14 days, max 60 |
| `GET /api/price-quotes?serviceTypeId=&discountCode=` | anyone | `ICustomerOrderService.QuoteAsync`: never uses up the code |
| `POST /api/orders` | anyone | `PlaceAsync` → 201, `Location: /api/orders/{reference}`; full date → 400 with `nextAvailableDate` |
| `GET /api/orders` | Customer | `GetMyOrdersAsync` |
| `GET /api/orders/{reference}` | owner, or guest with `Order-Contact` header | `GetAsync` |
| `PATCH /api/orders/{reference}` | same | `PatchAsync`: JSON Patch, `/notes` only, while New |

**Orders, staff side** (implemented test-first: `AdminOrderQueryApiTests`, `AdminOrderUpdateApiTests`, `AdminOrderLifecycleApiTests`):

| Route | Access | Service method |
|---|---|---|
| `GET /api/admin/orders?search=&status=&serviceTypeId=&dueFrom=&dueTo=&sort=&page=` | staff | `IAdminOrderService.SearchAsync` |

| `GET /api/admin/orders/{id}` | staff | `GetAsync` |
| `PATCH /api/admin/orders/{id}` | staff | `PatchAsync`: details, service, due date, `/internalNotes`, `/discountCode` |
| `DELETE /api/admin/orders/{id}` | Admin | `DeleteAsync` → 204 |
| `GET` · `POST /api/admin/orders/{id}/status-changes` | staff | `GetStatusChangesAsync`, `AddStatusChangeAsync` → 201 + `Location`; invalid transition → 400 |
| `GET /api/admin/orders/{id}/status-changes/{changeId}` | staff | `GetStatusChangeAsync` |
| `GET /api/admin/orders/{id}/price-override` | staff | `GetPriceOverrideAsync`; 404 when none is set |
| `PUT` · `DELETE /api/admin/orders/{id}/price-override` | Admin | `SetPriceOverrideAsync` → **201 when created, 200 when replaced** (RFC 9110); `ClearPriceOverrideAsync` → 204, also when none is set (idempotent) |

**Users** (test-first: `AccountApiTests`, `StaffApiTests`):

| Route | Access | Service method |
|---|---|---|
| `POST /api/users` | anyone | `IAccountService.RegisterAsync`: a Customer account → 201, `Location: /api/users/me`, signed in; taken email → 409 |
| `GET /api/users/me` | logged in | `GetCurrentAsync` |
| `POST /api/session` | anyone | `LoginAsync` → 200 with the user and a cookie; wrong email or password → 401 (same answer for both) |
| `DELETE /api/session` | anyone | `LogoutAsync` → 204 |
| `GET` · `POST /api/admin/staff`, `GET /api/admin/staff/{id}` | Admin | `GetStaffAsync`, `CreateStaffAsync` → 201, `GetStaffMemberAsync` |

The session is a resource without its own URI (it lives in the cookie), so logging in is a `POST` answered with 200 and the signed-in user, which RFC 9110 allows for a `POST` whose response describes the result. Every 401 carries a `WWW-Authenticate` challenge (RFC 9110 requires one).

"Staff" means Admin or Mechanic. Deleting orders and overriding prices are Admin-only.

**Why these shapes:**
- **Guests prove access with an `Order-Contact` request header** (email or phone on the order), not a query parameter or a `POST /lookup`: the order stays a plain `GET` resource, and personal data stays out of URLs, browser history and logs. No `X-` prefix (RFC 6648). A wrong or missing contact gives 404, so a guessed reference doesn't reveal that an order exists.
- **A quote is a `GET`**: it's safe and idempotent, a calculation, not a new resource.
- **Status changes are a sub-collection (`POST …/status-changes`)**, not a patched field: each change is a history entry with its own author, time and reason.
- **The price override is a singleton sub-resource (`PUT`/`DELETE`)**: Admin-only, so it can't share the staff `PATCH`.
- **`/api/admin/…` is the staff view** of the same orders: the routing-based separation the assignment asks for.
- In a `POST`, a broken business rule is 400. Only `PATCH` uses 422 (RFC 5789).
- **Admin search** matches the reference (exact, any case, prefix optional), email (exact, ignoring case), phone (exact, any format), and customer name or bike brand (substring, ignoring case). Email and phone are value objects stored as whole values, so a partial match would need extra search columns; exact matching keeps the model simple and covers how staff look people up.
- **A staff `PATCH` applies only what it changes**: internal notes can still be edited on a completed order, while details can't. Moving the due date re-checks capacity (the order doesn't count against itself); a full date is 422 with `nextAvailableDate`.

### Errors

All errors are RFC 7807 ProblemDetails:

| Status | When | Extra fields |
|---|---|---|
| 400 | Request validation failed (DataAnnotations on the request record) | `errors`: field → messages |
| 400 | Business rule violated (`DomainException`) | anything in `DomainException.Details`, e.g. `nextAvailableDate` when a due date is full |
| 401 / 403 | Not logged in / wrong role | |
| 404 | `NotFoundException`, including an order the caller may not see | |
| 409 | `ConflictException`: duplicate, or a failed JSON Patch `test` | |
| 415 | Wrong `Content-Type` (e.g. `application/json` for a JSON Patch) | |
| 422 | `UnprocessableException`: a JSON Patch that can't be applied, or that would leave the resource invalid | `errors`: field → messages |

Enums are sent as strings (`"InProgress"`), dates as `yyyy-MM-dd`.
