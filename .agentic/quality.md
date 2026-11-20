# Quality

## Definition of done
- Acceptance criteria met and demonstrated.
- Unit tests for rules, integration tests for each adapter, one end-to-end test per key flow.
- CI green: build, tests, lint, dependency and secret scan.
- Docs in `.agentic/` updated in the same change.

## Service levels
| SLI | SLO |
|---|---|
| API success rate | 99.9% over 30 days |
| p95 API latency (list, pages) | < 300 ms |
| Download success rate (quarter-end weeks) | 99.9% |
| p95 download time to first byte | < 500 ms |
| Audit-write failures | 0 (any failure pages someone; downloads fail closed) |
| Revocation effective | next request (no cache) |
| Notification delivered to the in-app inbox | 99% within 60 s of publish |

## Gates specific to this product
- Tenant isolation: `TenantFilterTests` and `TenantIsolationTests` must pass; never skip or weaken them.
- Module boundaries: `ModuleBoundaryTests`.
- Privacy guard and brand grep clean before every push.