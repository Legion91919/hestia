using Hestia.Tenancy;

namespace Hestia.Unit.Tests;

public class TenancyTests
{
    private sealed record Note(TenantId TenantId, string Text) : ITenantOwned;

    private sealed class FixedContext(TenantId? tenant) : ITenantContext
    {
        public TenantId? Current { get; set; } = tenant;
    }

    [Theory]
    [InlineData("kestrel-trust", true)]
    [InlineData("a1", true)]
    [InlineData("", false)]
    [InlineData("../etc", false)]
    [InlineData("Kestrel", false)]
    [InlineData("a/b", false)]
    public void Tenant_ids_are_safe_slugs(string value, bool valid) =>
        Assert.Equal(valid, TenantId.TryParse(value, out _));

    [Fact]
    public void Store_without_a_tenant_refuses_every_operation()
    {
        var store = new TenantScopedStore<string, Note>(new TenantPartitionedData<string, Note>(), new FixedContext(null));
        Assert.Throws<TenantRequiredException>(() => store.Find("n1"));
        Assert.Throws<TenantRequiredException>(() => store.List());
        Assert.Throws<TenantRequiredException>(() => store.TryAdd("n1", new Note(TenantId.Parse("a1"), "x")));
        Assert.Throws<TenantRequiredException>(() => store.Upsert("n1", new Note(TenantId.Parse("a1"), "x")));
    }

    [Fact]
    public void Tenants_never_see_each_others_rows_even_with_the_same_key()
    {
        var data = new TenantPartitionedData<string, Note>();
        var a = new TenantScopedStore<string, Note>(data, new FixedContext(TenantId.Parse("tenant-a")));
        var b = new TenantScopedStore<string, Note>(data, new FixedContext(TenantId.Parse("tenant-b")));
        Assert.True(a.TryAdd("n1", new Note(TenantId.Parse("tenant-a"), "for a")));

        Assert.Null(b.Find("n1"));
        Assert.Empty(b.List());
        Assert.Equal("for a", a.Find("n1")!.Text);
    }

    [Fact]
    public void Writing_a_row_for_another_tenant_is_refused()
    {
        var store = new TenantScopedStore<string, Note>(new TenantPartitionedData<string, Note>(), new FixedContext(TenantId.Parse("tenant-a")));
        Assert.Throws<TenantMismatchException>(() => store.Upsert("n1", new Note(TenantId.Parse("tenant-b"), "sneaky")));
    }
}
