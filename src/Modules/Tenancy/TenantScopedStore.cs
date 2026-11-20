using System.Collections.Concurrent;

namespace Hestia.Tenancy;

/// <summary>A row that belongs to exactly one tenant.</summary>
public interface ITenantOwned
{
    /// <summary>The owning tenant.</summary>
    TenantId TenantId { get; }
}

/// <summary>
/// Marker for every repository and store of tenant-owned data. Implementations get their data only through
/// <see cref="TenantScopedStore{TKey,T}"/>; architecture tests fail otherwise.
/// </summary>
#pragma warning disable CA1040 // Marker interface on purpose: architecture tests find repositories by it.
public interface ITenantRepository
#pragma warning restore CA1040
{
}

/// <summary>
/// The rows of every tenant, partitioned by tenant. Its members are internal: modules can only reach the data through
/// <see cref="TenantScopedStore{TKey,T}"/>. With a database this becomes a global query filter or row-level security.
/// </summary>
public sealed class TenantPartitionedData<TKey, T>
    where TKey : notnull
    where T : ITenantOwned
{
    internal ConcurrentDictionary<(TenantId Tenant, TKey Key), T> Rows { get; } = new();
}

/// <summary>
/// The central tenant filter. Every operation first requires a current tenant, reads only that tenant's partition and
/// refuses writes for any other tenant. There is no method that reads across tenants.
/// </summary>
public sealed class TenantScopedStore<TKey, T>(TenantPartitionedData<TKey, T> data, ITenantContext context)
    where TKey : notnull
    where T : ITenantOwned
{
    /// <summary>The current tenant's row with this key, or default.</summary>
    public T? Find(TKey key)
    {
        var tenant = context.Require();
        return data.Rows.TryGetValue((tenant, key), out var row) ? row : default;
    }

    /// <summary>The current tenant's rows.</summary>
    public IReadOnlyList<T> List()
    {
        var tenant = context.Require();
        return data.Rows.Where(r => r.Key.Tenant == tenant).Select(r => r.Value).ToList();
    }

    /// <summary>Adds a row for the current tenant; false when the key exists.</summary>
    public bool TryAdd(TKey key, T row)
    {
        var tenant = Own(row);
        return data.Rows.TryAdd((tenant, key), row);
    }

    /// <summary>Adds or replaces a row for the current tenant.</summary>
    public void Upsert(TKey key, T row)
    {
        var tenant = Own(row);
        data.Rows[(tenant, key)] = row;
    }

    private TenantId Own(T row)
    {
        var tenant = context.Require();
        ArgumentNullException.ThrowIfNull(row);
        if (row.TenantId != tenant) throw new TenantMismatchException();
        return tenant;
    }
}
