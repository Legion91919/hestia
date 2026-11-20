using System.Collections.Concurrent;
using Hestia.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Hestia.Investors;

/// <summary>An investor entity (one tenant), for example a family trust or a pension fund.</summary>
/// <param name="TenantId">The tenant.</param>
/// <param name="Name">Display name.</param>
public sealed record Investor(TenantId TenantId, string Name);

/// <summary>Whether a user may still reach the investor's documents.</summary>
public enum InvestorUserStatus
{
    /// <summary>Has access.</summary>
    Active,

    /// <summary>Access withdrawn by investor relations.</summary>
    Revoked,
}

/// <summary>A person who may act for an investor. Holds no contact details: those stay in the identity store.</summary>
/// <param name="TenantId">The investor (tenant).</param>
/// <param name="UserId">The user id from the identity store.</param>
/// <param name="DisplayName">Short display name.</param>
/// <param name="Status">Access status.</param>
/// <param name="RevokedAt">When access was revoked.</param>
public sealed record InvestorUser(TenantId TenantId, string UserId, string DisplayName, InvestorUserStatus Status, DateTimeOffset? RevokedAt) : ITenantOwned
{
    /// <summary>True while the user may reach documents.</summary>
    public bool HasAccess => Status == InvestorUserStatus.Active;

    /// <summary>Withdraws access. Revoking again keeps the first revocation time.</summary>
    public InvestorUser Revoke(DateTimeOffset at) => Status == InvestorUserStatus.Revoked ? this : this with { Status = InvestorUserStatus.Revoked, RevokedAt = at };

    /// <summary>Gives access back.</summary>
    public InvestorUser Restore() => this with { Status = InvestorUserStatus.Active, RevokedAt = null };
}

/// <summary>The current tenant's users.</summary>
public interface IInvestorUserRepository : ITenantRepository
{
    /// <summary>The user, if they belong to the current tenant.</summary>
    InvestorUser? Find(string userId);

    /// <summary>All users of the current tenant.</summary>
    IReadOnlyList<InvestorUser> List();

    /// <summary>Saves a user of the current tenant.</summary>
    void Save(InvestorUser user);
}

/// <summary>
/// The list of investors (tenants) itself. Cross-tenant by nature, holds no documents or people, and is used only by
/// IR endpoints and seeding.
/// </summary>
public interface IInvestorRegistry
{
    /// <summary>All investors, by name.</summary>
    IReadOnlyList<Investor> All();

    /// <summary>One investor.</summary>
    Investor? Find(TenantId tenant);

    /// <summary>Adds or renames an investor.</summary>
    void Register(Investor investor);
}

internal sealed class InvestorUserRepository(TenantScopedStore<string, InvestorUser> store) : IInvestorUserRepository
{
    public InvestorUser? Find(string userId) => store.Find(userId);

    public IReadOnlyList<InvestorUser> List() => store.List().OrderBy(u => u.DisplayName, StringComparer.Ordinal).ToList();

    public void Save(InvestorUser user) => store.Upsert(user?.UserId!, user!);
}

internal sealed class InvestorRegistry : IInvestorRegistry
{
    private readonly ConcurrentDictionary<TenantId, Investor> _investors = new();

    public IReadOnlyList<Investor> All() => _investors.Values.OrderBy(i => i.Name, StringComparer.Ordinal).ToList();

    public Investor? Find(TenantId tenant) => _investors.GetValueOrDefault(tenant);

    public void Register(Investor investor)
    {
        ArgumentNullException.ThrowIfNull(investor);
        _investors[investor.TenantId] = investor;
    }
}

/// <summary>Registers the Investors module.</summary>
public static class InvestorsModule
{
    /// <summary>Adds the module's services.</summary>
    public static IServiceCollection AddInvestorsModule(this IServiceCollection services)
    {
        services.AddSingleton<IInvestorRegistry, InvestorRegistry>();
        services.AddScoped<IInvestorUserRepository, InvestorUserRepository>();
        return services;
    }
}
