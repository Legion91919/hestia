using Hestia.Tenancy;

namespace Hestia.Documents;

/// <summary>What a document is.</summary>
public enum DocumentKind
{
    /// <summary>Quarterly capital account statement.</summary>
    Statement,

    /// <summary>Capital call notice.</summary>
    CapitalCall,

    /// <summary>Any other investor notice.</summary>
    Notice,
}

/// <summary>A document published to one investor (tenant). The file itself lives in tenant-partitioned storage.</summary>
/// <param name="Id">Client-supplied id (makes publishing idempotent).</param>
/// <param name="TenantId">The investor that owns it.</param>
/// <param name="Title">Title.</param>
/// <param name="Fund">Fund name.</param>
/// <param name="Period">Quarter, for example <c>Q3 2026</c>.</param>
/// <param name="Kind">Kind.</param>
/// <param name="FileName">Download file name.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="SizeBytes">File size.</param>
/// <param name="Sha256">Hex SHA-256 of the file.</param>
/// <param name="PublishedAt">When it was published (UTC).</param>
/// <param name="PublishedBy">User id of the publisher.</param>
public sealed record Document(
    Guid Id, TenantId TenantId, string Title, string Fund, string Period, DocumentKind Kind, string FileName, string ContentType,
    long SizeBytes, string Sha256, DateTimeOffset PublishedAt, string PublishedBy) : ITenantOwned;

/// <summary>Publish a document to the current tenant.</summary>
/// <param name="Id">Client-supplied id; retrying with the same id and file is safe.</param>
/// <param name="Title">Title.</param>
/// <param name="Fund">Fund.</param>
/// <param name="Period">Quarter, <c>Q1 2026</c> to <c>Q4 2026</c>.</param>
/// <param name="Kind">Kind.</param>
/// <param name="FileName">File name without any path.</param>
/// <param name="ContentType"><c>application/pdf</c> or <c>text/plain</c>.</param>
/// <param name="Content">File bytes.</param>
#pragma warning disable CA1819 // A command carrying file bytes; copied once into storage.
public sealed record PublishDocument(Guid Id, string Title, string Fund, string Period, DocumentKind Kind, string FileName, string ContentType, byte[] Content);
#pragma warning restore CA1819

/// <summary>Result of a publish.</summary>
/// <param name="Document">The stored document.</param>
/// <param name="Created">False when this was a safe retry of an earlier publish.</param>
public sealed record PublishOutcome(Document Document, bool Created);

/// <summary>An opened download. Dispose the stream.</summary>
/// <param name="Document">The document.</param>
/// <param name="Content">The file stream.</param>
public sealed record DocumentDownload(Document Document, Stream Content);

/// <summary>Port: told after a document is first published (the host sends notifications).</summary>
public interface IDocumentPublishedHandler
{
    /// <summary>Called once per newly published document, inside the publishing tenant's scope.</summary>
    Task OnPublishedAsync(Document document, CancellationToken cancellationToken);
}

/// <summary>Storage settings (<c>Documents</c> section).</summary>
public sealed class DocumentStorageOptions
{
    /// <summary>Root folder; files go to <c>&lt;Root&gt;/&lt;tenantId&gt;/&lt;documentId&gt;.&lt;ext&gt;</c>.</summary>
    public string Root { get; set; } = "App_Data/documents";

    /// <summary>Largest accepted file.</summary>
    public long MaxBytes { get; set; } = 10 * 1024 * 1024;
}
