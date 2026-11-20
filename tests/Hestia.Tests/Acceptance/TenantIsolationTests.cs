using System.Net.Http.Json;
using System.Text.Json;
using Hestia.Api;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hestia.Acceptance.Tests;

/// <summary>Slice 1, criterion 1: investor A never sees investor B's documents, on any endpoint.</summary>
public class TenantIsolationTests
{
    [Fact]
    public async Task Each_investor_lists_only_their_own_documents()
    {
        await using var host = await TestApp.StartAsync();
        var a = await TestApp.ListDocumentsAsync(host, await TestApp.LoginAsync(host, TestApp.InvestorA));
        var b = await TestApp.ListDocumentsAsync(host, await TestApp.LoginAsync(host, TestApp.InvestorB));

        Assert.NotEmpty(a);
        Assert.NotEmpty(b);
        var aIds = a.Select(d => d.GetProperty("id").GetString()).ToHashSet();
        Assert.DoesNotContain(b, d => aIds.Contains(d.GetProperty("id").GetString()));
        Assert.All(a, d => Assert.Equal(DemoData.KestrelTenant, d.GetProperty("investor").GetString()));
    }

    [Fact]
    public async Task Investor_b_cannot_reach_investor_a_data_through_any_endpoint()
    {
        await using var host = await TestApp.StartAsync();
        var tokenA = await TestApp.LoginAsync(host, TestApp.InvestorA);
        var tokenB = await TestApp.LoginAsync(host, TestApp.InvestorB);
        var aDocs = (await TestApp.ListDocumentsAsync(host, tokenA)).Select(d => d.GetProperty("id").GetString()!).ToList();
        var aUserId = (await (await host.SendAsync(HttpMethod.Get, "/auth/me", bearer: tokenA)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var endpoints = host.App.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/api/", StringComparison.Ordinal)).ToList();
        Assert.True(endpoints.Count >= 8, $"expected the full API surface, found {endpoints.Count}");

        var checkedRoutes = 0;
        foreach (var endpoint in endpoints)
        {
            var methods = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods ?? ["GET"];
            var path = Fill(endpoint.RoutePattern.RawText!, aDocs[0], aUserId);
            foreach (var method in methods)
            {
                var response = await host.SendAsync(new HttpMethod(method), path, method == "GET" ? null : new { }, bearer: tokenB);
                // Problem details echo the requested path as "instance"; that is the caller's own input, not a leak.
                var body = (await response.Content.ReadAsStringAsync()).Replace(path, string.Empty, StringComparison.Ordinal);
                Assert.All(aDocs, id => Assert.DoesNotContain(id, body, StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(aUserId, body, StringComparison.Ordinal);
                if (path.StartsWith("/api/ir/", StringComparison.Ordinal) || path.StartsWith("/api/audit/", StringComparison.Ordinal))
                {
                    Assert.Equal(403, (int)response.StatusCode);
                }
                else if (path.Contains(aDocs[0], StringComparison.Ordinal))
                {
                    Assert.True((int)response.StatusCode is 404 or 405, $"{method} {path} returned {(int)response.StatusCode}");
                }

                checkedRoutes++;
            }
        }

        Assert.True(checkedRoutes >= 8);
        Assert.Equal(aDocs.Count, (await TestApp.ListDocumentsAsync(host, tokenA)).Count);
    }

    [Fact]
    public async Task The_documents_page_never_shows_another_investors_documents()
    {
        await using var host = await TestApp.StartAsync();
        var aDocs = (await TestApp.ListDocumentsAsync(host, await TestApp.LoginAsync(host, TestApp.InvestorA))).Select(d => d.GetProperty("id").GetString()!).ToList();
        using var browserB = TestApp.Browser(host, await TestApp.LoginAsync(host, TestApp.InvestorB));

        var html = await browserB.GetStringAsync("/documents");

        Assert.Contains("Marula Pension Fund", html, StringComparison.Ordinal);
        Assert.All(aDocs, id => Assert.DoesNotContain(id, html, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Kestrel Family Trust", html, StringComparison.Ordinal);
    }

    private static string Fill(string pattern, string docId, string userId)
    {
        var path = pattern;
        foreach (var (name, value) in new[] { ("id", docId), ("documentId", docId), ("tenantId", DemoData.KestrelTenant), ("userId", userId), ("actor", userId), ("type", "document") })
        {
            path = System.Text.RegularExpressions.Regex.Replace(path, "\\{" + name + "(:[^}]*)?\\}", value);
        }

        return path;
    }
}
