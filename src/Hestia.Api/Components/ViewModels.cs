using System.Globalization;
using Agentic.Blocks.Audit;
using Agentic.Blocks.Auth;
using Agentic.Blocks.Core;
using Hestia.Documents;
using Hestia.Investors;
using Hestia.Tenancy;

namespace Hestia.Api.Components;

/// <summary>One document row on the investor's page.</summary>
public sealed record DocumentRowModel(Guid Id, string Title, string Fund, string Period, string KindLabel, string PublishedLabel, string SizeLabel, bool IsNew)
{
    /// <summary>Status in words, never colour alone.</summary>
    public string StatusLabel => IsNew ? "New" : "Opened";

    /// <summary>Audited download link.</summary>
    public string DownloadUrl => $"/api/documents/{Id}/download";
}

/// <summary>What <c>Pages/Documents.razor</c> renders: the newest statement as the hero, then everything else newest first.</summary>
public sealed record DocumentsPageModel(string InvestorName, DocumentRowModel? Hero, IReadOnlyList<DocumentRowModel> Earlier)
{
    /// <summary>True when there is nothing to show (empty state).</summary>
    public bool IsEmpty => Hero is null;

    /// <summary>Builds the model. <paramref name="opened"/> holds ids this user has downloaded (from the access log).</summary>
    public static DocumentsPageModel Build(string investorName, IReadOnlyList<Document> documents, IReadOnlySet<Guid> opened)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(opened);
        var ordered = documents.OrderByDescending(d => d.PublishedAt).ThenByDescending(d => PeriodKey(d.Period)).ThenBy(d => d.Kind).ToList();
        var hero = ordered.FirstOrDefault(d => d.Kind == DocumentKind.Statement) ?? ordered.FirstOrDefault();
        var rows = ordered.Where(d => d != hero).Select(d => Row(d, opened)).ToList();
        return new DocumentsPageModel(investorName, hero is null ? null : Row(hero, opened), rows);
    }

    private static DocumentRowModel Row(Document d, IReadOnlySet<Guid> opened) =>
        new(d.Id, d.Title, d.Fund, d.Period, Kind(d.Kind), d.PublishedAt.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), Size(d.SizeBytes), !opened.Contains(d.Id));

    private static string Kind(DocumentKind kind) => kind switch
    {
        DocumentKind.Statement => "Statement",
        DocumentKind.CapitalCall => "Capital call",
        _ => "Notice",
    };

    internal static string Size(long bytes) => bytes >= 1024 * 1024
        ? (bytes / 1024d / 1024d).ToString("0.0", CultureInfo.InvariantCulture) + " MB"
        : Math.Max(1, (long)Math.Ceiling(bytes / 1024d)).ToString(CultureInfo.InvariantCulture) + " KB";

    private static int PeriodKey(string period) =>
        period.Length == 7 && int.TryParse(period.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out var year) ? (year * 10) + (period[1] - '0') : 0;
}

/// <summary>Loads <see cref="DocumentsPageModel"/> for the signed-in investor.</summary>
public sealed class DocumentsPageLoader(IDocumentService documents, IAuditLog audit, IInvestorRegistry registry, ITenantContext tenant)
{
    /// <summary>Loads the page model.</summary>
    public async Task<DocumentsPageModel> LoadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var current = tenant.Require();
        var history = await audit.GetByActorAsync(userId, new PageRequest(limit: PageRequest.MaxLimit), cancellationToken);
        var opened = history.IsSuccess
            ? history.Value.Items.Where(e => e.Action == DocumentAudit.Downloaded && Guid.TryParse(e.EntityId, out _)).Select(e => Guid.Parse(e.EntityId)).ToHashSet()
            : [];
        return DocumentsPageModel.Build(registry.Find(current)?.Name ?? current.Value, documents.List(), opened);
    }
}

/// <summary>One user row in the IR console.</summary>
public sealed record IrUserRow(string TenantId, string UserId, string DisplayName, string Email, bool HasAccess);

/// <summary>One investor in the IR console.</summary>
public sealed record IrInvestorRow(string TenantId, string Name, int DocumentCount, IReadOnlyList<IrUserRow> Users);

/// <summary>One access-log line in the IR console.</summary>
public sealed record IrAccessRow(long Sequence, string When, string Who, string Investor, string What);

/// <summary>What <c>Pages/IrConsole.razor</c> renders.</summary>
public sealed record IrConsoleModel(IReadOnlyList<IrInvestorRow> Investors, IReadOnlyList<IrAccessRow> RecentAccess);

/// <summary>Loads the IR console: every investor (each read in its own tenant scope) and the newest downloads.</summary>
public sealed class IrConsoleLoader(IInvestorRegistry registry, ITenantScopeFactory tenants, IUserStore identities, IAuditStore auditStore)
{
    /// <summary>Loads the console model.</summary>
    public async Task<IrConsoleModel> LoadAsync(CancellationToken cancellationToken = default)
    {
        var investors = new List<IrInvestorRow>();
        var titles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var investor in registry.All())
        {
            await using var scope = tenants.CreateScope(investor.TenantId);
            var docs = scope.ServiceProvider.GetRequiredService<IDocumentService>().List();
            foreach (var d in docs) titles[d.Id.ToString()] = $"{d.Title}, {d.Period}";
            var users = new List<IrUserRow>();
            foreach (var u in scope.ServiceProvider.GetRequiredService<IInvestorUserRepository>().List())
            {
                users.Add(new IrUserRow(investor.TenantId.Value, u.UserId, u.DisplayName, await EmailAsync(u.UserId, cancellationToken), u.HasAccess));
            }

            investors.Add(new IrInvestorRow(investor.TenantId.Value, investor.Name, docs.Count, users));
        }

        var names = registry.All().ToDictionary(i => i.TenantId.Value, i => i.Name, StringComparer.Ordinal);
        var access = new List<IrAccessRow>();
        foreach (var e in (await auditStore.QueryAsync(new AuditQuery(EntityType: DocumentAudit.EntityType), null, 50, cancellationToken)).Where(e => e.Action == DocumentAudit.Downloaded))
        {
            var tenant = e.Reason?.StartsWith(DocumentAudit.TenantReasonPrefix, StringComparison.Ordinal) == true ? e.Reason[DocumentAudit.TenantReasonPrefix.Length..] : "?";
            access.Add(new IrAccessRow(e.Sequence, e.Timestamp.ToString("dd MMM yyyy HH:mm:ss 'UTC'", CultureInfo.InvariantCulture), await EmailAsync(e.Actor, cancellationToken),
                names.GetValueOrDefault(tenant, tenant), titles.GetValueOrDefault(e.EntityId, e.EntityId)));
        }

        return new IrConsoleModel(investors, access);
    }

    private async Task<string> EmailAsync(string userId, CancellationToken ct) => (await identities.FindByIdAsync(userId, ct))?.Email ?? userId;
}
