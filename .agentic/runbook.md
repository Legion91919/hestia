# Runbook

## Run locally
```bash
export Demo__Password='choose-a-local-demo-password'   # optional; otherwise one is generated and logged once
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Hestia.Api   # http://localhost:5203
```
The web UI is served by the same app (Blazor SSR): open `http://localhost:5203/login`. Invented demo users (Development only):
`ayanda@kestrel.example.com`, `lerato@kestrel.example.com` (Kestrel Family Trust), `pieter@marula.example.com` (Marula Pension Fund),
`thandi@oyster.example.com` (Oyster Bay Endowment, no documents: empty state), `ir@harbourline.example.com` (IR console).
Documents are written under `src/Hestia.Api/App_Data/documents/<tenantId>/` (git-ignored); set `Documents__Root` to move them.

API quick check:
```bash
T=$(curl -s -XPOST localhost:5203/auth/login -H 'content-type: application/json' \
  -d "{\"email\":\"ayanda@kestrel.example.com\",\"password\":\"$Demo__Password\"}" | python3 -c 'import sys,json;print(json.load(sys.stdin)["accessToken"])')
curl -s localhost:5203/api/documents -H "authorization: Bearer $T"
```

## Build and test
```bash
dotnet build Hestia.sln -c Release      # warnings are errors; includes the Blazor components
dotnet test Hestia.sln -c Release       # unit, architecture and real-HTTP acceptance tests
```
Building blocks restore from `eng/packages` (vendored, ADR-0002) through `nuget.config`; xUnit comes from nuget.org.
Offline workspaces (no nuget.org): build each `src` project with `--configfile <offline config>` and run the tests with the kit's
offline xUnit runner, passing `src/Hestia.Api/Hestia.Api.csproj` and a reference project for `Agentic.Blocks.Testing`.

## Deploy
Not deployed yet. Target: one container per region (SA or EU), HTTPS only, `ASPNETCORE_ENVIRONMENT=Production`, persisted Data
Protection key ring, `Documents__Root` on durable storage (slice 2: blob storage). Never set `Demo__Seed` in production.

## Monitor and alerts
- `/health/live` (process) and `/health/ready` (dependencies).
- Page on any `document.audit_unavailable` problem (downloads are being refused) and on download success < 99% for 10 minutes.
- Every response carries `X-Correlation-Id`; the same id is in the audit entry.

## Common incidents
- **"Who opened statement X?"** IR console access log, or `GET /api/audit/entities/document/{id}` as an IR user.
- **Cut off a user now:** IR console "Revoke access", or `POST /api/ir/investors/{tenantId}/users/{userId}/revoke`. Effective on the next request.
- **Investor sees no documents:** check their `tenant_id` and that they are listed (Active) under that investor in the IR console.
- **Everyone signed out after a deploy:** the Data Protection key ring was not persisted.
