using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Hestia.Api;

namespace Hestia.Acceptance.Tests;

/// <summary>A new statement is stored per tenant, is idempotent on its client id, and notifies that investor's users only.</summary>
public class PublishAndNotifyTests
{
    private static object Body(string content = "%PDF-1.4\n% invented Q4 statement\n%%EOF") => new
    {
        title = "Capital account statement",
        fund = "Harbourline Credit Fund II",
        period = "Q4 2026",
        kind = "Statement",
        fileName = "statement-q4-2026.pdf",
        contentType = "application/pdf",
        contentBase64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(content)),
    };

    [Fact]
    public async Task Publishing_notifies_the_investors_users_in_app_and_nobody_else()
    {
        await using var host = await TestApp.StartAsync();
        var ir = await TestApp.LoginAsync(host, TestApp.Ir);
        var a = await TestApp.LoginAsync(host, TestApp.InvestorA);
        var b = await TestApp.LoginAsync(host, TestApp.InvestorB);
        async Task<int> Unread(string token) => (await (await host.SendAsync(HttpMethod.Get, "/api/notifications/inbox/unread-count", bearer: token)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("unread").GetInt32();
        var aBefore = await TestApp.EventuallyAsync(() => Unread(a), n => n > 0);
        var bBefore = await Unread(b);

        var id = Guid.NewGuid();
        var response = await host.SendAsync(HttpMethod.Put, $"/api/ir/investors/{DemoData.KestrelTenant}/documents/{id}", Body(), bearer: ir);

        Assert.Equal(201, (int)response.StatusCode);
        Assert.Equal(aBefore + 1, await TestApp.EventuallyAsync(() => Unread(a), n => n > aBefore));
        Assert.Equal(bBefore, await Unread(b));
        var inbox = await (await host.SendAsync(HttpMethod.Get, "/api/notifications/inbox", bearer: a)).Content.ReadAsStringAsync();
        Assert.Contains("Q4 2026", inbox, StringComparison.Ordinal);
        Assert.Contains(await TestApp.ListDocumentsAsync(host, a), d => d.GetProperty("id").GetString() == id.ToString());
    }

    [Fact]
    public async Task Retrying_the_same_publish_is_safe_and_a_different_file_with_the_same_id_conflicts()
    {
        await using var host = await TestApp.StartAsync();
        var ir = await TestApp.LoginAsync(host, TestApp.Ir);
        var id = Guid.NewGuid();
        var path = $"/api/ir/investors/{DemoData.KestrelTenant}/documents/{id}";

        Assert.Equal(201, (int)(await host.SendAsync(HttpMethod.Put, path, Body(), bearer: ir)).StatusCode);
        Assert.Equal(200, (int)(await host.SendAsync(HttpMethod.Put, path, Body(), bearer: ir)).StatusCode);
        Assert.Equal(409, (int)(await host.SendAsync(HttpMethod.Put, path, Body("%PDF-1.4\n% different\n"), bearer: ir)).StatusCode);
    }

    [Fact]
    public async Task Files_are_stored_under_a_tenant_partitioned_path()
    {
        string? root = null;
        await using var host = await TestApp.StartAsync();
        root = host.App.Configuration["Documents:Root"]!;
        var id = Guid.NewGuid();
        await host.SendAsync(HttpMethod.Put, $"/api/ir/investors/{DemoData.KestrelTenant}/documents/{id}", Body(), bearer: await TestApp.LoginAsync(host, TestApp.Ir));

        Assert.True(File.Exists(Path.Combine(root, DemoData.KestrelTenant, $"{id:N}.pdf")));
        Assert.False(File.Exists(Path.Combine(root, DemoData.MarulaTenant, $"{id:N}.pdf")));
    }

    [Fact]
    public async Task Invalid_uploads_get_problem_details()
    {
        await using var host = await TestApp.StartAsync();
        var response = await host.SendAsync(HttpMethod.Put, $"/api/ir/investors/{DemoData.KestrelTenant}/documents/{Guid.NewGuid()}", Body("not a pdf"), bearer: await TestApp.LoginAsync(host, TestApp.Ir));
        Assert.Equal(400, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }
}
