# AI workflow

## Tools

<!-- TODO -->

## Where AI provided value

<!-- TODO -->

## Where AI was less suitable or required correction

<!-- TODO -->

## Decisions handled manually, and why

<!-- TODO -->

## Example: prompt / agent task that helped

<!-- TODO -->

## Example: something reviewed, corrected, or rejected

<!-- TODO -->

## Log

- **Project setup** (Claude Code): generated `flake.nix` for the dev shell based on my NixOS config, and scaffolded the backend/frontend.
  - The first backend scaffold used a conventional `src/BouvetBike.Api` + `tests/` layout. I asked for DDD, rejected the proposed multi-project split (Domain/Application/Infrastructure/Api) as too heavy, and chose a single project with DDD folders. An architecture test keeps the domain free of EF/ASP.NET.
  - While checking that `AddValidation()` worked, it found that request types declared inside `Program.cs` are silently not validated (requests with invalid input return 200). Types in their own files under a namespace are validated. A temporary probe endpoint and test confirmed this, and the finding was recorded in `CLAUDE.md`.
- **Domain models** (Claude Code): I chose the model design through discussion. I rejected the AI's first, minimal proposal (contact info as value objects, no users) in favour of real Users with Identity. I then dropped its separate Customer entity, since a customer is a User. Re-reading the requirements together showed that guests are the spec's default, which led to the contact snapshot and the reference + email/phone lookup rule. The AI then implemented the models, EF mapping and tests.
  - Caught by tests, not by review: `Money` used get-only properties, which EF Core can't map, so it failed at runtime with "No suitable constructor". The domain unit tests all passed; only the persistence round-trip test exposed it. This is why there's a persistence test, not just domain tests.
  - Caught in review: the first `ReplaceLines` and `UpdateDetails` changed state before all validation had run, so a failed call could leave an order half-updated. Both were reordered to validate first.
- **Restructure into modules** (Claude Code): asked whether DDD fits a real product, the AI said yes for the core (orders, scheduling, pricing) but not for the CRUD-like parts, and suggested organising by business area. I set the per-module folder names (`Api`, `Contracts`, `Application`, `Persistence`, plus `Models`). I accepted its suggestion to call `Models` `Domain` instead, since in .NET "Models" usually means DTOs.
  - I pushed back on over-modelling: of the 9 entities, 6 were folded into the order or dropped (bike brand → string, saved bikes, order lines → one service, notes → two text fields, per-date capacity). I chose to keep the status history as the audit trail. Everything was rebuilt and the tests ported.
  - The architecture tests now encode the module rules, and each rule was checked against a deliberate violation before relying on it.
- **Contracts first** (Claude Code): before any endpoint, the AI defined the request/response records, the application service interfaces and the error types, and mapped every planned route to one service method in `docs/architecture.md`. It changed one earlier plan in the process: `MapIdentityApi` was dropped in favour of four auth endpoints of our own.
  - Caught in review: `DueDateFullException` first declared its own `Details` with `new`, hiding the base property. The exception handler reads `DomainException.Details`, so the suggested date would have silently vanished from the response. It was fixed to set the base property, and a handler test now checks that `nextAvailableDate` reaches the JSON.
- **Interfaces into Contracts** (my call): I questioned having the service interfaces in `Application/`, and whether `Contracts/` should be called `Dtos/`. The AI argued against a separate `Interfaces/` folder (grouping by construct, not meaning) and laid out two options. I chose to treat `Contracts/` as each module's public surface (DTOs + interfaces), with `Application/` holding the private implementations. That made a new rule enforceable: modules may only use each other's `Contracts` and `Domain`.
- **Test-first, starting with the catalog** (Claude Code): I chose TDD and asked for a REST API compliant with RFC 6902 (JSON Patch). The AI pointed out that `PATCH` status codes come from RFC 5789 (400/409/415/422), and that the planned order endpoints (`/lookup`, `/quote`, `/status`) aren't RESTful and need reworking. It then wrote the catalog contracts and 34 failing API tests: CRUD, JSON Patch ops including `test` and atomicity, content type, auth per role, and 405 for `DELETE`. It also added a test auth handler so tests can act as any role without real login.
  - The red run was checked for *why* each test fails: every catalog failure is a 404 from the missing endpoint. Two tests (`Get_Unknown_Is404`, `Patch_Unknown_Is404`) pass already for the wrong reason, because the route itself is missing. They only become meaningful once the endpoint exists.
- **Catalog implemented** (Claude Code): the AI implemented the catalog service, endpoints, JSON Patch handling and cookie auth with role policies until the red tests passed. All 113 were green on the first run.
  - Green on the first try was treated as a reason to look harder, not as proof. The AI ran the real app in Development to check what the tests can't: they use a test login, so they can't show that the real cookie auth returns 401 instead of the cookie handler's default redirect to a login page. It did return 401, `DELETE` gave 405, and the OpenAPI document advertises `application/json-patch+json`.
- **Orders made RESTful, then red tests** (Claude Code): before writing the order tests, the AI redesigned the planned order endpoints as REST resources. `POST /lookup` became `GET /api/orders/{reference}` with an `Order-Contact` header (keeps PII out of URLs; RFC 6648, so no `X-` prefix). The quote became `GET /api/price-quotes`. Status changes became a `status-changes` sub-collection, and the Admin-only price override a `PUT`/`DELETE` sub-resource. It then wrote 41 tests for capacity, quotes, placing and getting orders, with the app clock fixed to 2026-03-02 so date rules are deterministic.
  - Red run: all 39 failures are 404s from the missing endpoints. The two `Get_UnknownReference_Is404` cases pass already for the wrong reason (the route is missing).
- **Capacity, quotes and placing orders implemented** (Claude Code): the first run left 3 of 154 tests red, and all three were faults in the tests, not the code:
  - Eight orders shared one due date, so the capacity rule (correctly) rejected the sixth. Fixed with a `FreshDay()` helper, and the rule went into `CLAUDE.md`.
  - A test required a relative `Location` header. The API sends an absolute one, which RFC 9110 also allows, so the test was over-specified and now checks that it points at the order.
  - After the fixes, the AI ran the real app with `curl` and spotted a bug no test covered: timestamps read back from SQLite came without the `Z` (SQLite drops `DateTime.Kind`), so browsers would show them in the wrong time zone. A failing test was written first, then fixed with a UTC convention for every `DateTime`.
- **Staff order API: search, get, patch, delete** (Claude Code): of 48 new tests, 43 passed on the first implementation run. All 5 failures were in the tests again:
  - Four compared response records that contain lists. C# record equality compares lists by reference, so identical responses never matched. Fixed with an `AssertSameJson` helper.
  - One compared sorted results with an unsorted expectation.

  Pattern so far: the AI's test code fails more often than its production code, on test plumbing (shared data, equality, ordering) rather than on the behaviour being specified. Every red-after-implementation run is therefore checked to see whether the code or the test is wrong before anything is changed.
- **Remaining domains, one step at a time** (Claude Code, autonomously): I asked the AI to do the rest of the domains with the same workflow and to stop if it was unsure. It ran four red → green cycles: customer notes and my orders, staff search/get/patch/delete, status changes and price override, then accounts/sessions/staff. Each step got its own commits.
  - Decisions it took without stopping, because they follow conventions already agreed, and recorded in `docs/architecture.md`:
    - REST shapes for auth (`POST /api/users`, `POST`/`DELETE /api/session`)
    - 201 vs 200 for the first and later price-override `PUT`s (RFC 9110 "MUST")
    - a `WWW-Authenticate` challenge on every 401 (RFC 9110)
    - exact-match search for email and phone
    - invalid status transitions as 400, per our `POST` rule. It flagged 409 as a defensible alternative.
  - Every post-implementation failure was again a test-plumbing fault (record equality on lists, twice), not a product bug.
  - Test infrastructure changed so requests without the test header use the real cookie sign-in, which lets the auth tests run the real flow. The full staff flow (login, search, status change, price override, logout) was then checked against the running app with `curl`.
- **Discount codes: demo data and admin API** (Claude Code): I chose to do both options the AI offered: demo codes (`WELCOME10`, `SPRING100`) seeded only in Development, with a test that guards against them reaching other environments, and an Admin API with the same REST shape as the catalog.
  - The first green run instead failed 225 tests: the app wouldn't start, because two modules named an endpoint `GetById` and endpoint names must be unique. One readable startup error explained every failure. The handlers were renamed after their resource, and the rule went into `CLAUDE.md`.
- **400 vs 409 for refused operations** (discussion, my decision): the AI flagged that invalid status transitions returned 400 under our "broken rule in a `POST` = 400" convention, and that 409 was defensible. We discussed it before changing anything. I framed it as: how should we answer when the request is correct but the operation isn't allowed? The AI split that into *why* it's refused:
  - input breaks a rule → 400/422
  - who you are → 403
  - this resource never supports the method → 405
  - **this resource's current state forbids it → 409**

  We kept rules about *other* resources (full due date, used-up code) as 400, since reloading the order wouldn't help and the response says what to change. I chose this option ("B") over keeping one rule ("A") or making all rule failures 409 ("C"). It was implemented test-first: a `StateConflictException` subtype in the domain (still HTTP-agnostic), 18 tests turned red for the right reason, then green.
- **Frontend** (Claude Code): I asked for the whole site in light mode, with centralized CSS, reused components, and inspiration from common web UIs, and said frontend tests weren't needed. The AI built a token-based stylesheet, ~15 reusable components and 11 pages against the existing API. It kept two smoke tests so `just test` still covers the frontend.
  - Verified visually, not just by compiling: the AI ran the real backend and frontend and drove a headless browser (Playwright) through every page, booking, staff login and order actions included, taking screenshots. That found three problems the typechecker couldn't:
    - an empty booking showed a raw "failed to read JSON" error, because `""` was sent for an unchosen service and date (now `null`, so the API returns field errors)
    - the mobile header wrapped badly
    - the logo touched the screen edge on mobile
  - Corrected in review: the AI's first UI copy contained business claims nobody gave it ("parts are quoted separately", "you pay when you pick up the bike"). These were replaced with statements that are true of the system.
- **Guest lookup reworked** (my decision, after testing the app myself): finding my booking with only my email "did not work". The AI reproduced it in a browser before changing anything. Reference + email worked, but email alone sent the request to the logged-in "my orders" endpoint and showed a raw "status 401", so the page's requirement (both fields) was neither enforced nor explained.
  - I decided the lookup should follow the assignment literally: the reference alone opens an order; email or phone alone lists the whole booking history. This reverses an earlier AI-proposed rule (reference *and* contact). The AI stated the privacy trade-off once (anyone who knows an email or phone can see those bookings) and suggested rate limiting as the mitigation. It also pointed out that `requirements.md` already claimed rate limiting, which had never been implemented. Both are now done test-first (lookup tests, a 429 test) and the trade-off is documented.
  - Corrected along the way: perl substitutions with a literal `|` in the pattern (`string | null`) mangled two files. The compiler caught it. The rest of that page was rewritten whole rather than patched.
- **Guest orders linked on registration** (my request): registering now links guest orders with the same email to the new account. The AI flagged that without email verification this lets someone claim another person's guest orders. That's the same exposure as the email lookup I'd already accepted, so I kept it, and it's documented. It kept the modules decoupled: Users announces "customer registered" through `ICustomerAccountEvents` in its own contracts, Orders implements it, and an architecture test makes sure Users never references Orders. Test-first: 3 red (domain method, linking), with the phone-only and other-account cases written as guards that stay green.
- **Email confirmation and repeated password** (my request): registration now asks for the password twice (a UI check; the API doesn't need it) and requires a confirmed email before login. There's no email delivery, so as I asked, the frontend redirects straight to the confirmation page the email would link to. The API returns that link, documented as a stand-in. The AI moved guest-order linking from registration to email confirmation, so only a proven address claims orders once real email exists.
  - It noticed that Identity refuses an unconfirmed user *before* checking the password, which would reveal that an account exists. The service now checks the password first, counting failures toward lockout, and only a correct password gets the 403.
  - Caught in the browser, not by the first tests: the confirm page sometimes showed an error right after a successful confirmation. React StrictMode sends the request twice in development, and the second one lost Identity's concurrency check. The same would happen on a double click or when an email client pre-opens links. A test firing two confirmations at once reproduced it every time; the fix reloads the user, and a parallel confirmation counts as success.
