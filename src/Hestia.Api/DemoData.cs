using System.Security.Cryptography;
using System.Text;
using Agentic.Blocks.Auth;
using Agentic.Blocks.Core;
using Hestia.Documents;
using Hestia.Investors;
using Hestia.Tenancy;

namespace Hestia.Api;

/// <summary>Invented demo data. Harbourline Lending, its investors, people and numbers are fictional.</summary>
public static class DemoData
{
    /// <summary>Kestrel Family Trust.</summary>
    public const string KestrelTenant = "kestrel-trust";

    /// <summary>Marula Pension Fund.</summary>
    public const string MarulaTenant = "marula-pension";

    /// <summary>Oyster Bay Endowment (no documents yet: shows the empty state).</summary>
    public const string OysterTenant = "oyster-endowment";

    /// <summary>Investor user at Kestrel.</summary>
    public const string KestrelUserEmail = "ayanda@kestrel.example.com";

    /// <summary>Second investor user at Kestrel (use it to try revoking).</summary>
    public const string KestrelSecondUserEmail = "lerato@kestrel.example.com";

    /// <summary>Investor user at Marula.</summary>
    public const string MarulaUserEmail = "pieter@marula.example.com";

    /// <summary>Investor user at Oyster Bay.</summary>
    public const string OysterUserEmail = "thandi@oyster.example.com";

    /// <summary>Investor relations staff.</summary>
    public const string IrUserEmail = "ir@harbourline.example.com";

    /// <summary>The fund all demo investors hold.</summary>
    public const string Fund = "Harbourline Credit Fund II";

    internal static readonly (string Tenant, string Name)[] Investors =
    [
        (KestrelTenant, "Kestrel Family Trust"),
        (MarulaTenant, "Marula Pension Fund"),
        (OysterTenant, "Oyster Bay Endowment"),
    ];

    internal static readonly (string Email, string? Tenant, string DisplayName)[] Users =
    [
        (KestrelUserEmail, KestrelTenant, "Ayanda M."),
        (KestrelSecondUserEmail, KestrelTenant, "Lerato K."),
        (MarulaUserEmail, MarulaTenant, "Pieter V."),
        (OysterUserEmail, OysterTenant, "Thandi N."),
        (IrUserEmail, null, "IR desk"),
    ];

    internal static readonly (string Tenant, string Period, decimal Balance)[] Statements =
    [
        (KestrelTenant, "Q1 2026", 12_480_000.00m),
        (KestrelTenant, "Q2 2026", 12_915_250.40m),
        (KestrelTenant, "Q3 2026", 13_102_880.15m),
        (MarulaTenant, "Q1 2026", 48_200_000.00m),
        (MarulaTenant, "Q2 2026", 49_075_910.55m),
        (MarulaTenant, "Q3 2026", 50_310_402.90m),
    ];

    /// <summary>Stable ids so demo links survive restarts.</summary>
    internal static Guid StableId(string seed) => new(SHA256.HashData(Encoding.UTF8.GetBytes("hestia-demo:" + seed)).AsSpan(0, 16));
}

/// <summary>Seeds the invented investors, users and documents in Development (or when <c>Demo:Seed=true</c>).</summary>
internal sealed partial class DemoDataSeeder(
    IHostEnvironment env, IConfiguration config, AuthService auth, IInvestorRegistry registry, ITenantScopeFactory tenants, TimeProvider time,
    ILogger<DemoDataSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!env.IsDevelopment() && !config.GetValue<bool>("Demo:Seed")) return;
        var secret = config["Demo:Password"];
        if (string.IsNullOrEmpty(secret))
        {
            secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(15));
            LogGenerated(logger, secret);
        }

        foreach (var (tenant, name) in DemoData.Investors) registry.Register(new Investor(TenantId.Parse(tenant), name));

        foreach (var (email, tenant, displayName) in DemoData.Users)
        {
            var roles = tenant is null ? new[] { HestiaApp.IrRole } : ["investor"];
            var created = await auth.RegisterAsync(email, secret, roles, tenant, cancellationToken);
            if (created.IsFailure || tenant is null) continue;
            await using var scope = tenants.CreateScope(TenantId.Parse(tenant));
            scope.ServiceProvider.GetRequiredService<IInvestorUserRepository>()
                .Save(new InvestorUser(TenantId.Parse(tenant), created.Value.Id, displayName, InvestorUserStatus.Active, null));
        }

        foreach (var (tenant, period, balance) in DemoData.Statements)
        {
            var name = registry.Find(TenantId.Parse(tenant))!.Name;
            var pdf = StatementPdf.Create(name, DemoData.Fund, period, Money.Of(balance, "ZAR"), time.GetUtcNow());
            await PublishAsync(tenant, new PublishDocument(DemoData.StableId($"{tenant}:{period}"), "Capital account statement", DemoData.Fund, period,
                DocumentKind.Statement, $"statement-{period.Replace(' ', '-').ToLowerInvariant()}.pdf", "application/pdf", pdf), cancellationToken);
        }

        await PublishAsync(DemoData.KestrelTenant, new PublishDocument(DemoData.StableId("kestrel:call7"), "Capital call notice #7", DemoData.Fund, "Q3 2026",
            DocumentKind.CapitalCall, "capital-call-7.txt", "text/plain",
            Encoding.UTF8.GetBytes("Capital call notice #7 (invented).\nAmount due: ZAR 650,000.00 by 30 Sep 2026.\n")), cancellationToken);
        LogSeeded(logger, DemoData.Investors.Length, DemoData.Users.Length);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task PublishAsync(string tenant, PublishDocument document, CancellationToken cancellationToken)
    {
        await using var scope = tenants.CreateScope(TenantId.Parse(tenant));
        await scope.ServiceProvider.GetRequiredService<IDocumentService>().PublishAsync(document, "system:seed", cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Development only: demo users share the generated password {Password}. Set Demo:Password to choose one.")]
    private static partial void LogGenerated(ILogger logger, string password);

    [LoggerMessage(Level = LogLevel.Information, Message = "Development only: seeded {Investors} investors and {Users} users with invented data")]
    private static partial void LogSeeded(ILogger logger, int investors, int users);
}
