using Hestia.Api;

namespace Hestia.Acceptance.Tests;

/// <summary>Smoke checks that server-rendered Blazor pages contain the expected content.</summary>
public class PagesTests
{
    [Fact]
    public async Task Login_page_renders_a_form_with_an_antiforgery_token()
    {
        await using var host = await TestApp.StartAsync();
        using var browser = TestApp.Browser(host, null);
        var html = await browser.GetStringAsync("/login");
        Assert.Contains("Sign in", html, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", html, StringComparison.Ordinal);
        Assert.Contains("app.css", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Signed_out_visitors_are_sent_to_login()
    {
        await using var host = await TestApp.StartAsync();
        using var browser = TestApp.Browser(host, null);
        var response = await browser.GetAsync("/documents");
        Assert.Equal(302, (int)response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Investor_sees_the_latest_statement_first()
    {
        await using var host = await TestApp.StartAsync();
        using var browser = TestApp.Browser(host, await TestApp.LoginAsync(host, TestApp.InvestorA));
        var html = await browser.GetStringAsync("/documents");
        Assert.Contains("Kestrel Family Trust", html, StringComparison.Ordinal);
        Assert.Contains("Open statement", html, StringComparison.Ordinal);
        Assert.Contains("Q3 2026", html, StringComparison.Ordinal);
        Assert.Contains("Earlier documents", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ir_console_shows_investors_users_and_the_access_log()
    {
        await using var host = await TestApp.StartAsync();
        var investor = await TestApp.LoginAsync(host, TestApp.InvestorA);
        var doc = TestApp.FirstStatement(await TestApp.ListDocumentsAsync(host, investor)).GetProperty("id").GetString()!;
        await host.SendAsync(HttpMethod.Get, $"/api/documents/{doc}/download", bearer: investor);
        using var browser = TestApp.Browser(host, await TestApp.LoginAsync(host, TestApp.Ir));

        var html = await browser.GetStringAsync("/ir");

        Assert.Contains("Access log", html, StringComparison.Ordinal);
        Assert.Contains("Marula Pension Fund", html, StringComparison.Ordinal);
        Assert.Contains(DemoData.KestrelUserEmail, html, StringComparison.Ordinal);
        Assert.Contains("Revoke access", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Investors_cannot_open_the_ir_console()
    {
        await using var host = await TestApp.StartAsync();
        using var browser = TestApp.Browser(host, await TestApp.LoginAsync(host, TestApp.InvestorA));
        var response = await browser.GetAsync("/ir");
        Assert.Equal(403, (int)response.StatusCode);
    }

    [Fact]
    public async Task Health_endpoints_answer()
    {
        await using var host = await TestApp.StartAsync();
        Assert.Equal(200, (int)(await host.Client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(200, (int)(await host.Client.GetAsync("/health/ready")).StatusCode);
    }
}
