using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hestia.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Hestia.Architecture.Tests;

/// <summary>
/// The central tenant filter: every repository must go through <see cref="TenantScopedStore{TKey,T}"/>, and every
/// repository method must refuse to run when there is no tenant. A new repository that bypasses this fails here.
/// </summary>
public class TenantFilterTests
{
    private static readonly Assembly[] AppAssemblies =
    [
        typeof(TenantId).Assembly,
        typeof(Hestia.Investors.InvestorUser).Assembly,
        typeof(Hestia.Documents.Document).Assembly,
        typeof(Hestia.Api.HestiaApp).Assembly,
    ];

    /// <summary>Cross-tenant by design and reachable only from IR endpoints: the list of tenants itself.</summary>
    private static readonly string[] CrossTenantAllowList = ["Hestia.Investors.InvestorRegistry"];

    private static IEnumerable<Type> Types() => AppAssemblies.SelectMany(a => a.GetTypes()).Where(t => !t.IsDefined(typeof(CompilerGeneratedAttribute)));

    [Fact]
    public void Every_repository_or_store_is_tenant_scoped()
    {
        var offenders = Types()
            .Where(t => t.IsClass && !t.IsAbstract && (t.Name.EndsWith("Repository", StringComparison.Ordinal) || t.Name.EndsWith("Store", StringComparison.Ordinal) || t.Name.EndsWith("Registry", StringComparison.Ordinal)))
            .Where(t => t.Namespace != typeof(TenantId).Namespace)
            .Where(t => !typeof(ITenantRepository).IsAssignableFrom(t) && !CrossTenantAllowList.Contains(t.FullName))
            .Select(t => t.FullName!)
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Repositories_hold_no_data_of_their_own()
    {
        foreach (var repo in RepositoryImplementations())
        {
            var collections = repo.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(f => f.FieldType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(f.FieldType))
                .Select(f => $"{repo.Name}.{f.Name}")
                .ToList();
            Assert.Empty(collections);
            Assert.True(repo.GetConstructors().Any(c => c.GetParameters().Any(p => IsTenantAware(p.ParameterType))),
                $"{repo.Name} must take a TenantScopedStore or ITenantContext");
        }
    }

    [Fact]
    public async Task Every_repository_method_refuses_to_run_without_a_tenant()
    {
        await using var host = await TestApp.StartAsync();
        await using var scope = host.App.Services.CreateAsyncScope();   // no HTTP request, so no tenant
        var interfaces = Types().Where(t => t.IsInterface && t != typeof(ITenantRepository) && typeof(ITenantRepository).IsAssignableFrom(t)).ToList();
        Assert.NotEmpty(interfaces);
        foreach (var contract in interfaces)
        {
            var repo = scope.ServiceProvider.GetService(contract);
            Assert.True(repo is not null, $"{contract.Name} is not registered");
            foreach (var method in contract.GetMethods())
            {
                Assert.True(await RefusesWithoutTenant(repo!, method), $"{contract.Name}.{method.Name} ran without a tenant");
            }
        }
    }

    [Fact]
    public async Task The_detector_catches_a_leaky_repository()
    {
        var leaky = new LeakyRepository();
        Assert.False(await RefusesWithoutTenant(leaky, typeof(ILeakyRepository).GetMethod(nameof(ILeakyRepository.All))!));
    }

    internal interface ILeakyRepository : ITenantRepository
    {
        IReadOnlyList<string> All();
    }

    private sealed class LeakyRepository : ILeakyRepository
    {
        public IReadOnlyList<string> All() => ["every tenant's rows"];
    }

    private static IEnumerable<Type> RepositoryImplementations() =>
        Types().Where(t => t.IsClass && !t.IsAbstract && typeof(ITenantRepository).IsAssignableFrom(t) && t.Assembly != typeof(TenantFilterTests).Assembly);

    private static bool IsTenantAware(Type t) =>
        t == typeof(ITenantContext) || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(TenantScopedStore<,>));

    private static async Task<bool> RefusesWithoutTenant(object target, MethodInfo method)
    {
        var args = method.GetParameters().Select(p => Dummy(p.ParameterType)).ToArray();
        try
        {
            var result = method.Invoke(target, args);
            if (result is Task task) await task;
            if (result is IDisposable d) d.Dispose();
            return false;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is TenantRequiredException)
        {
            return true;
        }
        catch (TenantRequiredException)
        {
            return true;
        }
    }

    private static object? Dummy(Type t)
    {
        if (t == typeof(string)) return "x";
        if (t == typeof(CancellationToken)) return CancellationToken.None;
        if (t == typeof(Guid)) return Guid.NewGuid();
        if (t.IsArray) return Array.CreateInstance(t.GetElementType()!, 1);
        if (t.IsValueType) return Activator.CreateInstance(t);
        if (t.IsInterface || t.IsAbstract) return null;
        return RuntimeHelpers.GetUninitializedObject(t);
    }
}
