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
