# Handover

What the client needs to own this without us.

- [ ] Source, infrastructure-as-code and pipelines in the client's own accounts
- [ ] Every secret rotated and owned by the client
- [ ] Architecture and decisions explained (`architecture.md`, `decisions/`)
- [ ] Runbook rehearsed by a client engineer
- [ ] Known issues and next steps listed below
- [ ] AI agent setup documented so their team can keep using it

## Known issues
- All stores are in memory (users, documents metadata, audit, inbox); a restart loses them. Files survive on disk. Slice 2.
- Browser sessions end when the 30-minute access token expires (no refresh in the browser yet).
- MFA for investors is not built (product constraint; slice 3).

## Recommended next steps
- Slice 2 persistence, then MFA. See `backlog.md`.