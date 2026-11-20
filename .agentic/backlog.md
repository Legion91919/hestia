# Backlog

Vertical slices, smallest valuable first.

## Now
- [x] Slice 1: An investor signs in and downloads this quarter's statement; the download is logged
  - Given investor A, when they list documents, then they never see investor B's documents (automated test)
  - Given a download, then who, what and when is written to an append-only access log
  - Given a revoked user, then access stops immediately
  - Test notes: one acceptance test per criterion; seeded invented data.

## Next
- [x] Sketch leg: tokens + three variants of the slice-1 screen (`sketch-ui`)
- [x] CI configured (build, format, tests + coverage, vulnerable packages, gitleaks, privacy guard, readiness); first green run pending a push with all gates before the first feature commit

## Later
- [ ] Slice 2: PostgreSQL + blob storage behind the same ports; EF Core global tenant filter + row-level security; persisted Data Protection keys
- [ ] Slice 3: MFA for investors via an OIDC provider (ADR); browser session refresh
- [ ] Slice 3: capital accounts module with a cached read model per investor
- [ ] Email notice ("a new document is waiting", never the attachment) through the Notifications email channel
- [ ] IR access-log export (CSV) per investor and date range
