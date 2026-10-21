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
