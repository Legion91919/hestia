# Backlog

Vertical slices, smallest valuable first.

## Now
- [ ] Slice 1: An investor signs in and downloads this quarter's statement; the download is logged
  - Given investor A, when they list documents, then they never see investor B's documents (automated test)
  - Given a download, then who, what and when is written to an append-only access log
  - Given a revoked user, then access stops immediately
  - Test notes: one acceptance test per criterion; seeded invented data.

## Next
- [ ] Sketch leg: tokens + three variants of the slice-1 screen (`sketch-ui`)
- [ ] CI green with all gates before the first feature commit

## Later
