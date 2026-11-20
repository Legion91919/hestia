# 0002. Vendored building blocks

- Status: Accepted
- Date: 2026-11-20

## Context
Login, the audit log, notifications, the outbox and web defaults are needed by every product. The Agentic.Blocks
packages (0.1.0) provide them with only the .NET shared framework as a dependency. They are published to a private
feed that CI and a client's build agents may not reach.

## Decision
Reference `Agentic.Blocks.{Core,Web,Auth,Audit,Outbox,Notifications}` (and `Testing` in tests) as packages, and vendor
the exact `.nupkg` files into `eng/packages/`. `nuget.config` has two sources: `eng/packages` (mapped to `Agentic.Blocks.*`)
and nuget.org (xUnit and test tooling only).

## Alternatives considered
- Private feed: one more secret and a feed the client must own on day one.
- Copying block source into the repo: we would fork tested code and lose upgrades.

## Consequences
- The repo builds anywhere with no private feed. Upgrading a block is "drop a new .nupkg, bump the version".
- The binaries are in git (about 200 KB). Each is traceable to a tagged block release.
