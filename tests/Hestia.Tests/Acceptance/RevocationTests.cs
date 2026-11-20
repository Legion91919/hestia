using System.Net.Http.Json;
using System.Text.Json;
using Hestia.Api;

namespace Hestia.Acceptance.Tests;

/// <summary>Slice 1, criterion 3: a revoked user loses access immediately.</summary>
public class RevocationTests
{
    [Fact]
    public async Task A_revoked_user_loses_access_on_the_next_request_and_cannot_sign_in()
    {
        await using var host = await TestApp.StartAsync();
        var token = await TestApp.LoginAsync(host, TestApp.InvestorA);
        var docId = TestApp.FirstStatement(await TestApp.ListDocumentsAsync(host, token)).GetProperty("id").GetString()!;
        var userId = (await (await host.SendAsync(HttpMethod.Get, "/auth/me", bearer: token)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var ir = await TestApp.LoginAsync(host, TestApp.Ir);

        var revoke = await host.SendAsync(HttpMethod.Post, $"/api/ir/investors/{DemoData.KestrelTenant}/users/{userId}/revoke", new { }, bearer: ir);
        Assert.Equal(204, (int)revoke.StatusCode);

        Assert.Equal(401, (int)(await host.SendAsync(HttpMethod.Get, "/api/documents", bearer: token)).StatusCode);
        Assert.Equal(401, (int)(await host.SendAsync(HttpMethod.Get, $"/api/documents/{docId}/download", bearer: token)).StatusCode);
        using var browser = TestApp.Browser(host, token);
        var page = await browser.GetAsync("/documents");
        Assert.Equal(302, (int)page.StatusCode);
        Assert.StartsWith("/login", page.Headers.Location!.OriginalString, StringComparison.Ordinal);

        var login = await host.SendAsync(HttpMethod.Post, "/auth/login", TestApp.Credentials(TestApp.InvestorA));
        Assert.Equal(401, (int)login.StatusCode);
        Assert.Contains("auth.access_revoked", await login.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restoring_a_user_lets_them_sign_in_again()
    {
        await using var host = await TestApp.StartAsync();
        var token = await TestApp.LoginAsync(host, TestApp.InvestorA);
        var userId = (await (await host.SendAsync(HttpMethod.Get, "/auth/me", bearer: token)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var ir = await TestApp.LoginAsync(host, TestApp.Ir);
        await host.SendAsync(HttpMethod.Post, $"/api/ir/investors/{DemoData.KestrelTenant}/users/{userId}/revoke", new { }, bearer: ir);

        var restore = await host.SendAsync(HttpMethod.Post, $"/api/ir/investors/{DemoData.KestrelTenant}/users/{userId}/restore", new { }, bearer: ir);

        Assert.Equal(204, (int)restore.StatusCode);
        Assert.NotEmpty(await TestApp.ListDocumentsAsync(host, await TestApp.LoginAsync(host, TestApp.InvestorA)));
    }

    [Fact]
    public async Task Self_registration_is_not_offered()
    {
        await using var host = await TestApp.StartAsync();
        var response = await host.SendAsync(HttpMethod.Post, "/auth/register", new { email = "new.investor@example.com", password = "Another-pass-2026!" });
        Assert.Equal(404, (int)response.StatusCode);
    }
}
