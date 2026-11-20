using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Agentic.Blocks.Testing;
using Hestia.Api;
using Microsoft.Extensions.DependencyInjection;

namespace Hestia.Tests;

/// <summary>Starts the real composition root on a random port with seeded invented data.</summary>
internal static class TestApp
{
    public const string Password = "Harbour-demo-2026!";
    public const string InvestorA = DemoData.KestrelUserEmail;
    public const string InvestorB = DemoData.MarulaUserEmail;
    public const string Ir = DemoData.IrUserEmail;

    public static Task<KestrelTestHost> StartAsync(Action<IServiceCollection>? overrides = null) =>
        KestrelTestHost.StartAsync(
            b =>
            {
                b.Configuration["Demo:Password"] = Password;
                b.Configuration["Documents:Root"] = Path.Combine(Path.GetTempPath(), "hestia-tests", Guid.NewGuid().ToString("N"));
                HestiaApp.ConfigureServices(b);
                overrides?.Invoke(b.Services);
            },
            HestiaApp.Configure);

    public static Dictionary<string, string> Credentials(string email) => new() { ["email"] = email, ["password"] = Password };

    public static async Task<string> LoginAsync(KestrelTestHost host, string email)
    {
        var response = await host.SendAsync(HttpMethod.Post, "/auth/login", Credentials(email));
        Assert.Equal(200, (int)response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("accessToken").GetString()!;
    }

    public static JsonElement FirstStatement(IEnumerable<JsonElement> docs) => docs.First(d => d.GetProperty("kind").GetString() == "Statement");

    public static async Task<List<JsonElement>> ListDocumentsAsync(KestrelTestHost host, string token)
    {
        var response = await host.SendAsync(HttpMethod.Get, "/api/documents", bearer: token);
        Assert.Equal(200, (int)response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<JsonElement>>())!;
    }

    /// <summary>A browser-like client: session cookie instead of a bearer header, no automatic redirects.</summary>
    public static HttpClient Browser(KestrelTestHost host, string? token)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = host.Client.BaseAddress };
        if (token is not null) client.DefaultRequestHeaders.Add("Cookie", $"{HestiaApp.SessionCookieName}={token}");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        return client;
    }

    public static async Task<T> EventuallyAsync<T>(Func<Task<T>> probe, Func<T, bool> done, int timeoutMs = 5000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (true)
        {
            var value = await probe();
            if (done(value) || DateTime.UtcNow > until) return value;
            await Task.Delay(50);
        }
    }
}
