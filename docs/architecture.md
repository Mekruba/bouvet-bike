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
- **The first admin is seeded on startup** from configuration (user secrets or environment variables, never committed).
- Customers never need an account; see [requirements](requirements.md#customers-and-accounts).

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
- The status history is its own table, cascade-deleted with the order and auto-included when it's loaded.
- **SQLite caveats:** decimals are stored as TEXT, so money can't be summed or sorted in SQL (totals are computed in memory). `DateTimeOffset` can't be ordered in SQL, which is why timestamps are UTC `DateTime`.

## API

Contract-first: request/response records live in each module's `Contracts/`, together with the use-case interfaces; implementations live in `Application/`. Endpoints map a route to one service method and nothing else. Live docs: Scalar at http://localhost:5080/scalar/v1.

### Endpoints (planned)

| Route | Access | Service method |
|---|---|---|
| `GET /api/services` | anyone | `ICatalogService.GetActiveAsync` |
| `GET /api/orders/capacity?from=&days=` | anyone | `ICapacityService.GetAsync` |
| `POST /api/orders/quote` | anyone | `ICustomerOrderService.QuoteAsync` |
| `POST /api/orders` | anyone | `ICustomerOrderService.PlaceAsync` (201) |
| `POST /api/orders/lookup` | anyone | `ICustomerOrderService.LookupAsync` |
| `PUT /api/orders/{reference}/notes` | anyone (proves access) | `ICustomerOrderService.UpdateNotesAsync` |
| `GET /api/orders/mine` | Customer | `ICustomerOrderService.GetMyOrdersAsync` |
| `POST /api/auth/register` · `login` · `logout`, `GET /api/auth/me` | anyone | `IAccountService` |
| `GET /api/admin/orders?search=&status=&…` | staff | `IAdminOrderService.SearchAsync` |
| `GET` · `PUT /api/admin/orders/{id}` | staff | `GetAsync`, `UpdateAsync` |
| `DELETE /api/admin/orders/{id}` | Admin | `DeleteAsync` |
| `POST /api/admin/orders/{id}/status` | staff | `ChangeStatusAsync` |
| `PUT /api/admin/orders/{id}/internal-notes` | staff | `UpdateInternalNotesAsync` |
| `PUT` · `DELETE /api/admin/orders/{id}/discount` | staff | `ApplyDiscountAsync`, `RemoveDiscountAsync` |
| `PUT` · `DELETE /api/admin/orders/{id}/price-override` | Admin | `OverridePriceAsync`, `ClearPriceOverrideAsync` |
| `GET /api/admin/capacity?from=&days=` | staff | `ICapacityService.GetAsync` |
| `GET` · `POST /api/admin/staff` | Admin | `IAccountService.GetStaffAsync`, `CreateStaffAsync` |

"Staff" means Admin or Mechanic. Deleting orders and overriding prices are Admin-only.

**Lookup is a `POST`** so the email or phone number isn't in the URL, browser history or server logs.

### Errors

All errors are RFC 7807 ProblemDetails:

| Status | When | Extra fields |
|---|---|---|
| 400 | Request validation failed (DataAnnotations on the request record) | `errors`: field → messages |
| 400 | Business rule violated (`DomainException`) | anything in `DomainException.Details`, e.g. `nextAvailableDate` when a due date is full |
| 401 / 403 | Not logged in / wrong role | |
| 404 | `NotFoundException`, including an order the caller may not see | |

Enums are sent as strings (`"InProgress"`), dates as `yyyy-MM-dd`.
