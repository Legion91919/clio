# 0002. Vendored building blocks

- Status: Accepted
- Date: 2026-11-20

## Context
Clio needs login, ProblemDetails/health/correlation ids, an append-only audit trail and reliable hand-off events.
The Agentic.Blocks packages provide these with no third-party runtime dependencies, but there is no public feed.

## Decision
Use `Agentic.Blocks.{Core,Web,Auth,Audit,Outbox}` 0.1.0 (and `Testing` in tests). The `.nupkg` files are vendored in
`eng/packages/`; `nuget.config` maps `Agentic.Blocks.*` to that folder and everything else to nuget.org, so the repo
restores anywhere (CI included) without credentials.

## Alternatives considered
- Private GitHub Packages feed: needs a token in every clone and CI run; the client may not own that feed.
- Copy the source into the repo: easy to edit, but loses versioning and drifts from the shared blocks.

## Consequences
- Upgrading a block = replace the `.nupkg`, bump the version, run the tests.
- About 300 KB of binaries in git. Packages are BCL/shared-framework only, so the supply-chain surface stays small.
