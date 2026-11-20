# Changelog

## Unreleased
- 2026-11-20: Sketch leg and slice 1 (tenant-isolated statements, audited downloads, instant revocation, in-app notice on publish) with a Blazor SSR UI; product named Hestia; ADRs 0002-0004; design dossier.
- Frame, Map and Decide legs complete (product.md, slice 1, ADR-0001).
- 2026-11-20: Visual demo: `demo/` Playwright scenario, captioned GIF/MP4 and screenshots in `docs/media/`, README "See it in action". Fixed two UI bugs the review found: the Updates link rendered raw Razor (`Updates@if...`) and enhanced navigation fetched downloads twice (two access-log entries per open); download links now opt out with `data-enhance-nav="false"`.
