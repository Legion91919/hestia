using Agentic.Blocks.Audit;
using Agentic.Blocks.Auth;
using Agentic.Blocks.Core;
using Agentic.Blocks.Notifications;
using Agentic.Blocks.Outbox;
using Hestia.Documents;
using Hestia.Investors;
using Hestia.Tenancy;

namespace Hestia.Api.Services;

/// <summary>Notification vocabulary.</summary>
public static class Notify
{
    /// <summary>Category for new documents.</summary>
    public const string StatementsCategory = "statements";

    /// <summary>Template for a new document.</summary>
    public const string StatementTemplate = "statement-published";
}

/// <summary>Documents module port: tells every active user of the investor that a new document is ready (in-app).</summary>
internal sealed class NotifyInvestorUsersOnPublish(IInvestorUserRepository users, INotificationService notifications, IOutbox outbox) : IDocumentPublishedHandler
{
    public async Task OnPublishedAsync(Document document, CancellationToken cancellationToken)
    {
        var data = new Dictionary<string, string>(StringComparer.Ordinal) { ["title"] = document.Title, ["fund"] = document.Fund, ["period"] = document.Period };
        foreach (var user in users.List().Where(u => u.HasAccess))
        {
            await notifications.SendAsync(user.UserId, Notify.StatementsCategory, Notify.StatementTemplate, data, cancellationToken);
        }

        // This runs in a tenant scope, not the request scope, so it owns the commit of its outbox.
        await outbox.CommitAsync(cancellationToken);
    }
}

/// <summary>Investor relations actions, shared by the JSON API and the Blazor IR console.</summary>
public sealed class IrActions(ITenantScopeFactory tenants, IInvestorRegistry registry, AuthService auth, IAuditLog audit, TimeProvider time)
{
    /// <summary>Withdraws a user's access: marks them revoked and rotates their security stamp so every token dies now.</summary>
    public Task<Result> RevokeAsync(string tenant, string userId, string actor, CancellationToken cancellationToken = default) =>
        ChangeAccessAsync(tenant, userId, actor, revoke: true, cancellationToken);

    /// <summary>Gives access back. Old tokens stay dead; the user signs in again.</summary>
    public Task<Result> RestoreAsync(string tenant, string userId, string actor, CancellationToken cancellationToken = default) =>
        ChangeAccessAsync(tenant, userId, actor, revoke: false, cancellationToken);

    /// <summary>Publishes a document to one investor.</summary>
    public async Task<Result<PublishOutcome>> PublishAsync(string tenant, PublishDocument document, string actor, CancellationToken cancellationToken = default)
    {
        if (Investor(tenant) is not { } investor) return UnknownInvestor;
        await using var scope = tenants.CreateScope(investor.TenantId);
        return await scope.ServiceProvider.GetRequiredService<IDocumentService>().PublishAsync(document, actor, cancellationToken);
    }

    /// <summary>Generates an invented statement PDF and publishes it.</summary>
    public Task<Result<PublishOutcome>> PublishStatementAsync(string tenant, string period, decimal closingBalance, string actor, CancellationToken cancellationToken = default)
    {
        if (Investor(tenant) is not { } investor) return Task.FromResult(Result.Failure<PublishOutcome>(UnknownInvestor));
        var balance = Money.TryOf(closingBalance, "ZAR");
        if (balance.IsFailure) return Task.FromResult(Result.Failure<PublishOutcome>(balance.Error));
        var pdf = StatementPdf.Create(investor.Name, DemoData.Fund, period ?? string.Empty, balance.Value, time.GetUtcNow());
        var id = DemoData.StableId($"{tenant}:{period}:statement");
        return PublishAsync(tenant, new PublishDocument(id, "Capital account statement", DemoData.Fund, period ?? string.Empty, DocumentKind.Statement,
            $"statement-{(period ?? "x").Replace(' ', '-').ToLowerInvariant()}.pdf", "application/pdf", pdf), actor, cancellationToken);
    }

    private static Error UnknownInvestor => Error.NotFound("investor.not_found", "No such investor.");

    private Investor? Investor(string tenant) => TenantId.TryParse(tenant, out var id) ? registry.Find(id) : null;

    private async Task<Result> ChangeAccessAsync(string tenant, string userId, string actor, bool revoke, CancellationToken cancellationToken)
    {
        if (Investor(tenant) is not { } investor) return UnknownInvestor;
        await using var scope = tenants.CreateScope(investor.TenantId);
        var users = scope.ServiceProvider.GetRequiredService<IInvestorUserRepository>();
        if (users.Find(userId) is not { } user) return Error.NotFound("investor_user.not_found", "No such user for this investor.");

        users.Save(revoke ? user.Revoke(time.GetUtcNow()) : user.Restore());
        if (revoke) await auth.LogoutEverywhereAsync(userId, cancellationToken);
        await audit.AppendAsync(new AuditRecord(actor, revoke ? "investor_user.revoked" : "investor_user.restored", "user", userId, DocumentAudit.TenantReasonPrefix + tenant), cancellationToken);
        return Result.Success();
    }
}
