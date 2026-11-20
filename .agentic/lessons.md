# Lessons

Generic, reusable lessons only. No client names, data or secrets.

- ASP.NET Core resolves a single `IClaimsTransformation`: to add a per-request check next to a library's one, decorate it instead of registering a second one.
- Problem details echo the request path as `instance`; isolation tests that grep response bodies for foreign ids must strip the caller's own path first.
- Prove a central tenant filter twice: structurally (repositories hold no collections and depend on the filter) and behaviourally (call every repository method with no tenant and expect a refusal), plus a self-test that a leaky repository is caught.
- Blazor static SSR can share one opaque bearer token with API clients by carrying it in an HttpOnly cookie read by the bearer handler: one scheme, one revocation path.
- The offline xUnit runner only discovers test classes whose namespace ends in `.Tests`.
