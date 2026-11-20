using Microsoft.Extensions.DependencyInjection;

namespace Hestia.Tenancy;

/// <summary>Registers the tenant context, tenant scopes and the central tenant filter.</summary>
public static class TenancyModule
{
    /// <summary>Adds tenancy services.</summary>
    public static IServiceCollection AddTenancy(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddSingleton<ITenantScopeFactory, TenantScopeFactory>();
        services.AddSingleton(typeof(TenantPartitionedData<,>));
        services.AddScoped(typeof(TenantScopedStore<,>));
        return services;
    }
}
