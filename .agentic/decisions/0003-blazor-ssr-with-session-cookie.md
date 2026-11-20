# 0003. Blazor server-side rendering with a session cookie carrying the bearer token

- Status: Accepted
- Date: 2026-11-20

## Context
Investors visit once a quarter, mostly on a phone, to open one document. IR staff work on desktop. The team is two
.NET engineers. The Auth block issues opaque bearer tokens (data-protected), which browsers cannot send by themselves.

## Decision
- The UI is Razor components in the same ASP.NET Core host, rendered on the server (static SSR). Forms post back with
  antiforgery tokens. Only the Updates badge uses interactive server mode (one small circuit per open page).
- After sign-in, the same opaque token API clients use is stored in an HttpOnly, SameSite=Strict, Secure (on HTTPS)
  cookie. The bearer handler reads the header first, then the cookie. One authentication scheme, one revocation path.

## Alternatives considered
- React or Angular SPA: a second toolchain (npm) and a JSON API that must be secured for browsers (CORS, token storage).
- Blazor WebAssembly: multi-megabyte first load on mobile for a page used four times a year.
- Cookie authentication as a second scheme: two ticket formats and two revocation paths to keep in step.

## Consequences
- Fast first paint, no client bundle beyond `blazor.web.js`; pages work without JavaScript except the live badge.
- Sessions last as long as the access token (30 minutes); a refresh flow for browsers is a later slice.
- Interactive components hold server memory per open tab; keep them rare.
