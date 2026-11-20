using Hestia.Api.Components;
using Hestia.Documents;
using Hestia.Tenancy;

namespace Hestia.Unit.Tests;

/// <summary>The view model behind Pages/Documents.razor: tested without rendering the component.</summary>
public class DocumentsPageModelTests
{
    private static Document Doc(string title, string period, DocumentKind kind, int day, long size = 49_152) =>
        new(Guid.NewGuid(), TenantId.Parse("kestrel-trust"), title, "Harbourline Credit Fund II", period, kind, "f.pdf", "application/pdf", size, "hash",
            new DateTimeOffset(2026, 10, day, 8, 0, 0, TimeSpan.Zero), "ir");

    [Fact]
    public void Newest_statement_is_the_hero_and_the_rest_are_newest_first()
    {
        var q2 = Doc("Capital account statement", "Q2 2026", DocumentKind.Statement, 1);
        var q3 = Doc("Capital account statement", "Q3 2026", DocumentKind.Statement, 14);
        var notice = Doc("Capital call notice #7", "Q3 2026", DocumentKind.CapitalCall, 20);

        var model = DocumentsPageModel.Build("Kestrel Family Trust", [q2, notice, q3], opened: new HashSet<Guid> { q2.Id });

        Assert.False(model.IsEmpty);
        Assert.Equal(q3.Id, model.Hero!.Id);
        Assert.Equal([notice.Id, q2.Id], model.Earlier.Select(r => r.Id).ToList());
        Assert.True(model.Hero.IsNew);
        Assert.False(model.Earlier[1].IsNew);
        Assert.Equal("Opened", model.Earlier[1].StatusLabel);
    }

    [Fact]
    public void Labels_are_human_readable()
    {
        var row = DocumentsPageModel.Build("X", [Doc("Capital account statement", "Q3 2026", DocumentKind.Statement, 14, 49_152)], new HashSet<Guid>()).Hero!;
        Assert.Equal("14 Oct 2026", row.PublishedLabel);
        Assert.Equal("48 KB", row.SizeLabel);
        Assert.Equal($"/api/documents/{row.Id}/download", row.DownloadUrl);
        Assert.Equal("New", row.StatusLabel);
    }

    [Fact]
    public void No_documents_gives_the_empty_state()
    {
        var model = DocumentsPageModel.Build("X", [], new HashSet<Guid>());
        Assert.True(model.IsEmpty);
        Assert.Null(model.Hero);
    }

    [Fact]
    public void Without_any_statement_the_newest_document_is_the_hero()
    {
        var notice = Doc("Capital call notice #7", "Q3 2026", DocumentKind.CapitalCall, 2);
        Assert.Equal(notice.Id, DocumentsPageModel.Build("X", [notice], new HashSet<Guid>()).Hero!.Id);
    }
}
