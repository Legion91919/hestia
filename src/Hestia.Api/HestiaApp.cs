using Agentic.Blocks.Audit;
using Agentic.Blocks.Auth;
using Agentic.Blocks.Notifications;
using Agentic.Blocks.Outbox;
using Agentic.Blocks.Web;
using Hestia.Api.Components;
using Hestia.Api.Endpoints;
using Hestia.Api.Security;
using Hestia.Api.Services;
using Hestia.Documents;
using Hestia.Investors;
using Hestia.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;

namespace Hestia.Api;

/// <summary>The composition root, shared by <c>Program</c> and the acceptance tests.</summary>
public static class HestiaApp
{
    /// <summary>HttpOnly cookie that carries the bearer token for the server-rendered pages.</summary>
    public const string SessionCookieName = "hestia_session";

    /// <summary>Role of Harbourline investor relations staff.</summary>
    public const string IrRole = "ir";

    /// <summary>Policy: signed in and bound to an investor (tenant).</summary>
    public const string InvestorPolicy = "investor";

    /// <summary>Registers modules, blocks and the Blazor components.</summary>
    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;
        services.AddSingleton(TimeProvider.System);
        services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

        // Must come before AddBlocksAuth (which uses TryAdd): pages redirect to /login, APIs answer 401.
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, BrowserAwareAuthorizationResultHandler>();

        services.AddBlocksWeb()
            .AddBlocksOutbox()
            .AddBlocksAudit()
            .AddBlocksAuth(o => o.AccessTokenLifetime = TimeSpan.FromMinutes(30))
            .AddBlocksNotifications(o => o
                .AddTemplate(Notify.StatementTemplate, "New document: {{title}}, {{period}}", "{{title}} for {{fund}} ({{period}}) is ready in Hestia.")
                .SetCategoryDefaults(Notify.StatementsCategory, NotificationChannel.InApp));

        services.AddTenancy().AddInvestorsModule().AddDocumentsModule(o =>
        {
            o.Root = builder.Configuration["Documents:Root"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "documents");
        });

        services.AddScoped<IDocumentPublishedHandler, NotifyInvestorUsersOnPublish>();
        services.AddScoped<IrActions>();
        services.AddScoped<SessionService>();
        services.AddScoped<DocumentsPageLoader>();
        services.AddScoped<IrConsoleLoader>();
        services.AddHostedService<DemoDataSeeder>();

        RevokedUserGuard.Decorate(services);
        services.PostConfigure<BearerTokenOptions>(BearerTokenDefaults.AuthenticationScheme, SessionCookie.ReadTokenFromCookie);
        services.AddAuthorizationBuilder()
            .AddPolicy(InvestorPolicy, p => p.RequireAuthenticatedUser().RequireClaim(TenantClaims.ClaimType))
            .AddPolicy(IrRole, p => p.RequireAuthenticatedUser().RequireRole(IrRole));

        services.AddRazorComponents().AddInteractiveServerComponents();
        services.AddCascadingAuthenticationState();
    }

    /// <summary>Builds the request pipeline and maps endpoints and pages.</summary>
    public static void Configure(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseBlocksWeb();
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.UseBlocksOutbox();

        app.MapBlocksAuth("/auth").AddEndpointFilter(AuthEndpointGuard.InvokeAsync);
        app.MapBlocksNotifications("/api/notifications");
        app.MapBlocksAudit("/api/audit", requiredRole: IrRole);
        app.MapDocumentEndpoints();
        app.MapIrEndpoints();
        app.MapAccountEndpoints();

        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
    }
}
