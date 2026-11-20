# 0004. Tenant isolation enforced by one central filter

- Status: Accepted
- Date: 2026-11-20

## Context
One missing `WHERE tenant = ...` leaks an investor's statements to another investor. That is the worst failure this
product can have (POPIA breach, loss of the fund's trust).

## Decision
- The tenant is taken only from the signed-in user's `tenant_id` claim (Auth block), or from a DI scope created for one
  tenant by `ITenantScopeFactory` (IR actions after a role check, seeding).
- All tenant-owned data goes through `TenantScopedStore<TKey,T>` in the Tenancy kernel. Its data is partitioned by
  tenant and its raw collections are `internal`, so a module cannot read across tenants. Every operation calls
  `ITenantContext.Require()` first and refuses writes for another tenant.
- Architecture tests fail if a repository holds its own collections, does not depend on the store or the tenant
  context, or has any method that runs without a tenant. An acceptance test calls every `/api` endpoint as investor B
  with investor A's identifiers.
- Files live under `<root>/<tenantId>/`, and the path is built from the context, never from the request.

## Alternatives considered
- Per-query filters in each repository: one forgotten filter is a breach.
- Database per tenant: strongest isolation, but heavy for two engineers and a few hundred investors.

## Consequences
- With a database (slice 2) the store becomes an EF Core global query filter plus row-level security on the same claim.
- Cross-tenant reads exist only in `IInvestorRegistry` (the list of tenants) and in IR code that loops over tenant scopes.
