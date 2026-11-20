using System.Net.Http.Json;
using System.Text.Json;
using Agentic.Blocks.Audit;
using Agentic.Blocks.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Hestia.Acceptance.Tests;

/// <summary>Slice 1, criterion 2: every download writes who, what and when to an append-only access log.</summary>
public class AccessLogTests
{
    [Fact]
    public async Task A_download_writes_who_what_and_when_before_the_file_is_sent()
    {
        await using var host = await TestApp.StartAsync();
        var token = await TestApp.LoginAsync(host, TestApp.InvestorA);
        var me = await (await host.SendAsync(HttpMethod.Get, "/auth/me", bearer: token)).Content.ReadFromJsonAsync<JsonElement>();
        var doc = TestApp.FirstStatement(await TestApp.ListDocumentsAsync(host, token));
        var id = doc.GetProperty("id").GetString()!;
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        var response = await host.SendAsync(HttpMethod.Get, $"/api/documents/{id}/download", bearer: token);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(await response.Content.ReadAsByteArrayAsync()), StringComparison.Ordinal);

        var audit = host.App.Services.GetRequiredService<IAuditLog>();
        var entries = (await audit.GetByEntityAsync("document", id, new PageRequest(limit: 10))).Value.Items;
        var entry = Assert.Single(entries, e => e.Action == "document.downloaded");
        Assert.Equal(me.GetProperty("id").GetString(), entry.Actor);
        Assert.Equal("document.downloaded", entry.Action);
        Assert.True(entry.Timestamp >= before);
        Assert.True((await audit.VerifyIntegrityAsync()).IsSuccess);
    }

    [Fact]
    public async Task Ir_can_read_the_access_log_for_a_document_but_investors_cannot()
    {
        await using var host = await TestApp.StartAsync();
        var investor = await TestApp.LoginAsync(host, TestApp.InvestorA);
        var id = TestApp.FirstStatement(await TestApp.ListDocumentsAsync(host, investor)).GetProperty("id").GetString()!;
        await host.SendAsync(HttpMethod.Get, $"/api/documents/{id}/download", bearer: investor);

        var asInvestor = await host.SendAsync(HttpMethod.Get, $"/api/audit/entities/document/{id}", bearer: investor);
        var asIr = await host.SendAsync(HttpMethod.Get, $"/api/audit/entities/document/{id}", bearer: await TestApp.LoginAsync(host, TestApp.Ir));

        Assert.Equal(403, (int)asInvestor.StatusCode);
        Assert.Equal(200, (int)asIr.StatusCode);
        Assert.Contains("document.downloaded", await asIr.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_access_log_has_no_way_to_change_or_remove_entries()
    {
        var names = typeof(IAuditLog).GetMethods().Concat(typeof(IAuditStore).GetMethods()).Select(m => m.Name).ToList();
        Assert.DoesNotContain(names, n => n.StartsWith("Update", StringComparison.Ordinal) || n.StartsWith("Delete", StringComparison.Ordinal) || n.StartsWith("Remove", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_document_from_another_investor_is_not_found_and_nothing_is_logged()
    {
        await using var host = await TestApp.StartAsync();
        var aDoc = TestApp.FirstStatement(await TestApp.ListDocumentsAsync(host, await TestApp.LoginAsync(host, TestApp.InvestorA))).GetProperty("id").GetString()!;

        var response = await host.SendAsync(HttpMethod.Get, $"/api/documents/{aDoc}/download", bearer: await TestApp.LoginAsync(host, TestApp.InvestorB));

        Assert.Equal(404, (int)response.StatusCode);
        var audit = host.App.Services.GetRequiredService<IAuditLog>();
        Assert.DoesNotContain((await audit.GetByEntityAsync("document", aDoc, new PageRequest(limit: 10))).Value.Items, e => e.Action == "document.downloaded");
    }
}
