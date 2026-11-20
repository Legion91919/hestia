using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hestia.Tenancy;

/// <summary>The tenant of the current unit of work, or null when there is none.</summary>
public interface ITenantContext
{
    /// <summary>The current tenant, or null.</summary>
    TenantId? Current { get; }
}

/// <summary>Helpers for <see cref="ITenantContext"/>.</summary>
public static class TenantContextExtensions
{
    /// <summary>The current tenant, or <see cref="TenantRequiredException"/>. Every tenant-owned read and write calls this first.</summary>
    public static TenantId Require(this ITenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Current ?? throw new TenantRequiredException();
    }
}

/// <summary>Thrown when tenant-owned data is touched without a tenant. Always a bug, never a user error.</summary>
public sealed class TenantRequiredException : InvalidOperationException
{
    /// <summary>Creates the exception.</summary>
    public TenantRequiredException() : base("Tenant-owned data was accessed without a tenant.") { }

    /// <summary>Creates the exception with a message.</summary>
    public TenantRequiredException(string message) : base(message) { }

    /// <summary>Creates the exception with a message and cause.</summary>
    public TenantRequiredException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Thrown when code tries to write a row that belongs to a different tenant than the current one.</summary>
public sealed class TenantMismatchException : InvalidOperationException
{
    /// <summary>Creates the exception.</summary>
    public TenantMismatchException() : base("A row was written for a tenant other than the current one.") { }

    /// <summary>Creates the exception with a message.</summary>
    public TenantMismatchException(string message) : base(message) { }

    /// <summary>Creates the exception with a message and cause.</summary>
    public TenantMismatchException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Resolves the tenant from the signed-in user's <c>tenant_id</c> claim, unless this DI scope was created for a
/// specific tenant by <see cref="ITenantScopeFactory"/> (IR actions, seeding, background work).
/// </summary>
internal sealed class TenantContext(IHttpContextAccessor http) : ITenantContext
{
    private TenantId? _fixed;

    public TenantId? Current => _fixed ?? TenantClaims.FromPrincipal(http.HttpContext?.User);

    internal void Fix(TenantId tenant)
    {
        if (_fixed is not null && _fixed != tenant) throw new InvalidOperationException("This scope already belongs to another tenant.");
        _fixed = tenant;
    }
}

/// <summary>Reads the tenant claim.</summary>
public static class TenantClaims
{
    /// <summary>Claim type written by the Auth block.</summary>
    public const string ClaimType = "tenant_id";

    /// <summary>The tenant of an authenticated principal, or null.</summary>
    public static TenantId? FromPrincipal(ClaimsPrincipal? principal) =>
        principal?.Identity?.IsAuthenticated == true && TenantId.TryParse(principal.FindFirst(ClaimType)?.Value, out var tenant) ? tenant : null;
}

/// <summary>Creates a DI scope whose tenant is fixed. Only trusted callers (IR endpoints after a role check, seeding) use it.</summary>
public interface ITenantScopeFactory
{
    /// <summary>Creates a scope for <paramref name="tenant"/>. Dispose it when done.</summary>
    AsyncServiceScope CreateScope(TenantId tenant);
}

internal sealed class TenantScopeFactory(IServiceScopeFactory scopes) : ITenantScopeFactory
{
    public AsyncServiceScope CreateScope(TenantId tenant)
    {
        var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Fix(tenant);
        return scope;
    }
}
