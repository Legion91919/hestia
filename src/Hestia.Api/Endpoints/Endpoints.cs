using System.Security.Claims;
using Agentic.Blocks.Auth;
using Agentic.Blocks.Web;
using Hestia.Api.Security;
using Hestia.Api.Services;
using Hestia.Documents;
using Hestia.Investors;
using Hestia.Tenancy;
using Microsoft.AspNetCore.Antiforgery;

namespace Hestia.Api.Endpoints;

/// <summary>A document as the API shows it.</summary>
public sealed record DocumentDto(Guid Id, string Investor, string Title, string Fund, string Period, string Kind, string FileName, long SizeBytes, DateTimeOffset PublishedAt, string DownloadUrl)
{
    internal static DocumentDto From(Document d) =>
        new(d.Id, d.TenantId.Value, d.Title, d.Fund, d.Period, d.Kind.ToString(), d.FileName, d.SizeBytes, d.PublishedAt, $"/api/documents/{d.Id}/download");
}

/// <summary>Body of <c>PUT /api/ir/investors/{tenantId}/documents/{documentId}</c>.</summary>
public sealed record PublishRequest(string Title, string Fund, string Period, DocumentKind Kind, string FileName, string ContentType, string ContentBase64);

/// <summary>An investor user as IR sees it.</summary>
public sealed record InvestorUserDto(string UserId, string DisplayName, string Status);

/// <summary>An investor with its users.</summary>
public sealed record InvestorDto(string TenantId, string Name, IReadOnlyList<InvestorUserDto> Users);

internal static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        // Tenant comes from the signed-in user's tenant_id claim; the services below cannot run without it.
        var docs = app.MapGroup("/api/documents").WithTags("Documents").RequireAuthorization(HestiaApp.InvestorPolicy).RequireRateLimiting(BlocksRateLimitPolicies.Api);

        docs.MapGet("/", (IDocumentService documents) => Results.Ok(documents.List().Select(DocumentDto.From)));

        docs.MapGet("/{id:guid}", (Guid id, IDocumentService documents) => documents.Find(id).ToHttpResult(d => Results.Ok(DocumentDto.From(d))));

        docs.MapGet("/{id:guid}/download", async (Guid id, ClaimsPrincipal user, IDocumentService documents, HttpContext http, CancellationToken ct) =>
        {
            var download = await documents.OpenDownloadAsync(id, user.GetUserId()!, ct);
            if (download.IsFailure) return download.Error.ToProblem();
            http.Response.Headers.CacheControl = "no-store";
            return Results.File(download.Value.Content, download.Value.Document.ContentType, download.Value.Document.FileName);
        });
    }
}

internal static class IrEndpoints
{
    public static void MapIrEndpoints(this IEndpointRouteBuilder app)
    {
        var ir = app.MapGroup("/api/ir").WithTags("Investor relations").RequireAuthorization(HestiaApp.IrRole).RequireRateLimiting(BlocksRateLimitPolicies.Api);

        ir.MapGet("/investors", (IInvestorRegistry registry, ITenantScopeFactory tenants) => Results.Ok(registry.All().Select(i =>
        {
            using var scope = tenants.CreateScope(i.TenantId);
            var users = scope.ServiceProvider.GetRequiredService<IInvestorUserRepository>().List();
            return new InvestorDto(i.TenantId.Value, i.Name, users.Select(u => new InvestorUserDto(u.UserId, u.DisplayName, u.Status.ToString())).ToList());
        }).ToList()));

        ir.MapPut("/investors/{tenantId}/documents/{documentId:guid}", async (string tenantId, Guid documentId, PublishRequest body, ClaimsPrincipal user, IrActions actions, CancellationToken ct) =>
        {
            byte[] content;
            try
            {
                content = Convert.FromBase64String(body.ContentBase64 ?? string.Empty);
            }
            catch (FormatException)
            {
                return Agentic.Blocks.Core.Error.Validation("document.invalid", "contentBase64", "The file is not valid base64.").ToProblem();
            }

            var command = new PublishDocument(documentId, body.Title, body.Fund, body.Period, body.Kind, body.FileName, body.ContentType, content);
            var published = await actions.PublishAsync(tenantId, command, user.GetUserId()!, ct);
            return published.ToHttpResult(o => o.Created
                ? Results.Created($"/api/documents/{o.Document.Id}", DocumentDto.From(o.Document))
                : Results.Ok(DocumentDto.From(o.Document)));
        });

        ir.MapPost("/investors/{tenantId}/users/{userId}/revoke", async (string tenantId, string userId, ClaimsPrincipal user, IrActions actions, CancellationToken ct) =>
            (await actions.RevokeAsync(tenantId, userId, user.GetUserId()!, ct)).ToHttpResult());

        ir.MapPost("/investors/{tenantId}/users/{userId}/restore", async (string tenantId, string userId, ClaimsPrincipal user, IrActions actions, CancellationToken ct) =>
            (await actions.RestoreAsync(tenantId, userId, user.GetUserId()!, ct)).ToHttpResult());
    }
}

internal static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        // Sign-out from the page header: a form POST with an antiforgery token. Rotates the stamp so the token dies everywhere.
        app.MapPost("/account/logout", async (HttpContext http, AuthService auth, CancellationToken ct) =>
        {
            if (http.User.GetUserId() is { } userId) await auth.LogoutEverywhereAsync(userId, ct);
            SessionCookie.Delete(http);
            return Results.Redirect("/login");
        }).WithMetadata(new RequireAntiforgeryTokenAttribute());
    }
}
