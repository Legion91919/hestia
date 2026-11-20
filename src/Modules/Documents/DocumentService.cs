using System.Security.Cryptography;
using Agentic.Blocks.Audit;
using Agentic.Blocks.Core;
using Hestia.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hestia.Documents;

/// <summary>Documents of the current tenant.</summary>
public interface IDocumentService
{
    /// <summary>The current tenant's documents, newest first.</summary>
    IReadOnlyList<Document> List();

    /// <summary>One document of the current tenant; not found for any other tenant's id.</summary>
    Result<Document> Find(Guid id);

    /// <summary>Publishes to the current tenant. Idempotent on <see cref="PublishDocument.Id"/>.</summary>
    Task<Result<PublishOutcome>> PublishAsync(PublishDocument command, string publishedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes <c>document.downloaded</c> (who, what, when) to the append-only audit log and only then opens the file.
    /// If the audit write fails, the file is not served.
    /// </summary>
    Task<Result<DocumentDownload>> OpenDownloadAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>Audit vocabulary for documents.</summary>
public static class DocumentAudit
{
    /// <summary>Entity type.</summary>
    public const string EntityType = "document";

    /// <summary>Download action.</summary>
    public const string Downloaded = "document.downloaded";

    /// <summary>Publish action.</summary>
    public const string Published = "document.published";

    /// <summary>Reason prefix carrying the tenant (no personal data).</summary>
    public const string TenantReasonPrefix = "tenant:";
}

internal interface IDocumentRepository : ITenantRepository
{
    Document? Find(Guid id);

    IReadOnlyList<Document> List();

    bool TryAdd(Document document);
}

internal sealed class DocumentRepository(TenantScopedStore<Guid, Document> store) : IDocumentRepository
{
    public Document? Find(Guid id) => store.Find(id);

    public IReadOnlyList<Document> List() => store.List();

    public bool TryAdd(Document document) => store.TryAdd(document?.Id ?? Guid.Empty, document!);
}

/// <summary>Files on local disk under <c>&lt;root&gt;/&lt;tenantId&gt;/</c>; the tenant always comes from the context.</summary>
internal interface IDocumentFileStore : ITenantRepository
{
    Task WriteAsync(Guid id, string extension, byte[] content, CancellationToken cancellationToken);

    Stream OpenRead(Guid id, string extension);
}

internal sealed class LocalDocumentFileStore(ITenantContext tenant, IOptions<DocumentStorageOptions> options) : IDocumentFileStore
{
    public async Task WriteAsync(Guid id, string extension, byte[] content, CancellationToken cancellationToken)
    {
        var path = PathFor(id, extension);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        await File.WriteAllBytesAsync(temp, content, cancellationToken);
        File.Move(temp, path, overwrite: true);
    }

    public Stream OpenRead(Guid id, string extension) =>
        new FileStream(PathFor(id, extension), FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);

    private string PathFor(Guid id, string extension)
    {
        var current = tenant.Require();
        var root = Path.GetFullPath(options.Value.Root);
        var path = Path.GetFullPath(Path.Combine(root, current.Value, $"{id:N}.{extension}"));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidOperationException("Path escaped the storage root.");
        return path;
    }
}

internal sealed class DocumentService(
    ITenantContext tenant,
    IDocumentRepository documents,
    IDocumentFileStore files,
    IAuditLog audit,
    IEnumerable<IDocumentPublishedHandler> handlers,
    TimeProvider time,
    IOptions<DocumentStorageOptions> options) : IDocumentService
{
    private static readonly Error NotFound = Error.NotFound("document.not_found", "No such document.");

    public IReadOnlyList<Document> List() => documents.List().OrderByDescending(d => d.PublishedAt).ThenBy(d => d.Title, StringComparer.Ordinal).ToList();

    public Result<Document> Find(Guid id) => documents.Find(id) is { } doc ? Result.Success(doc) : Result.Failure<Document>(NotFound);

    public async Task<Result<PublishOutcome>> PublishAsync(PublishDocument command, string publishedBy, CancellationToken cancellationToken = default)
    {
        var owner = tenant.Require();
        var valid = DocumentRules.Validate(command, options.Value.MaxBytes);
        if (valid.IsFailure) return valid.Error;

        var hash = Convert.ToHexString(SHA256.HashData(command.Content));
        if (documents.Find(command.Id) is { } existing)
        {
            return existing.Sha256 == hash && existing.Title == command.Title && existing.Period == command.Period
                ? new PublishOutcome(existing, Created: false)
                : Error.Conflict("document.id_conflict", "A different document was already published with this id.");
        }

        var document = new Document(command.Id, owner, command.Title.Trim(), command.Fund.Trim(), command.Period, command.Kind, command.FileName,
            command.ContentType, command.Content.Length, hash, time.GetUtcNow(), publishedBy);
        await files.WriteAsync(document.Id, Extension(document), command.Content, cancellationToken);
        if (!documents.TryAdd(document)) return Result.Success(new PublishOutcome(documents.Find(command.Id)!, Created: false));

        await audit.AppendAsync(new AuditRecord(publishedBy, DocumentAudit.Published, DocumentAudit.EntityType, document.Id.ToString(), DocumentAudit.TenantReasonPrefix + owner), cancellationToken);
        foreach (var handler in handlers) await handler.OnPublishedAsync(document, cancellationToken);
        return new PublishOutcome(document, Created: true);
    }

    public async Task<Result<DocumentDownload>> OpenDownloadAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default)
    {
        var owner = tenant.Require();
        if (documents.Find(id) is not { } document) return NotFound;

        var logged = await audit.AppendAsync(new AuditRecord(actorUserId, DocumentAudit.Downloaded, DocumentAudit.EntityType, id.ToString(), DocumentAudit.TenantReasonPrefix + owner), cancellationToken);
        if (logged.IsFailure) return Error.Failure("document.audit_unavailable", "The download could not be recorded, so it was not served.");
        return new DocumentDownload(document, files.OpenRead(id, Extension(document)));
    }

    private static string Extension(Document d) => d.ContentType == "application/pdf" ? "pdf" : "txt";
}

/// <summary>Registers the Documents module.</summary>
public static class DocumentsModule
{
    /// <summary>Adds the module's services. Needs <c>AddTenancy</c> and <c>AddBlocksAudit</c>.</summary>
    public static IServiceCollection AddDocumentsModule(this IServiceCollection services, Action<DocumentStorageOptions>? configure = null)
    {
        var builder = services.AddOptions<DocumentStorageOptions>().Validate(o => !string.IsNullOrWhiteSpace(o.Root) && o.MaxBytes > 0, "Documents options are invalid.");
        if (configure is not null) builder.Configure(configure);
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentFileStore, LocalDocumentFileStore>();
        services.AddScoped<IDocumentService, DocumentService>();
        return services;
    }
}
