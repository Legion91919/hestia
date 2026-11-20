using System.Text;
using Hestia.Documents;

namespace Hestia.Unit.Tests;

public class DocumentRulesTests
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n%invented\n");

    private static PublishDocument Valid() =>
        new(Guid.NewGuid(), "Capital account statement", "Harbourline Credit Fund II", "Q3 2026", DocumentKind.Statement, "statement-q3-2026.pdf", "application/pdf", Pdf);

    [Fact]
    public void A_valid_statement_passes() => Assert.True(DocumentRules.Validate(Valid(), maxBytes: 1024).IsSuccess);

    [Theory]
    [InlineData("Q5 2026")]
    [InlineData("2026 Q3")]
    [InlineData("")]
    public void Period_must_be_a_quarter(string period) =>
        Assert.False(DocumentRules.Validate(Valid() with { Period = period }, 1024).IsSuccess);

    [Fact]
    public void A_file_claiming_to_be_pdf_must_start_with_the_pdf_signature() =>
        Assert.False(DocumentRules.Validate(Valid() with { Content = Encoding.ASCII.GetBytes("MZ not a pdf") }, 1024).IsSuccess);

    [Fact]
    public void Plain_text_is_allowed_but_other_types_are_not()
    {
        Assert.True(DocumentRules.Validate(Valid() with { ContentType = "text/plain", FileName = "notice.txt", Content = Encoding.UTF8.GetBytes("Notice") }, 1024).IsSuccess);
        Assert.False(DocumentRules.Validate(Valid() with { ContentType = "application/zip" }, 1024).IsSuccess);
    }

    [Fact]
    public void Oversized_or_empty_files_are_refused()
    {
        Assert.False(DocumentRules.Validate(Valid(), maxBytes: 4).IsSuccess);
        Assert.False(DocumentRules.Validate(Valid() with { Content = [] }, 1024).IsSuccess);
    }

    [Theory]
    [InlineData("../../secret.pdf")]
    [InlineData("a\\b.pdf")]
    [InlineData("")]
    public void File_names_cannot_carry_paths(string name) =>
        Assert.False(DocumentRules.Validate(Valid() with { FileName = name }, 1024).IsSuccess);

    [Fact]
    public void Generated_statement_is_a_pdf_with_the_period_and_balance()
    {
        var bytes = StatementPdf.Create("Kestrel Family Trust", "Harbourline Credit Fund II", "Q3 2026", Agentic.Blocks.Core.Money.Of(1250000.5m, "ZAR"), new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero));
        var text = Encoding.ASCII.GetString(bytes);
        Assert.StartsWith("%PDF-1.4", text, StringComparison.Ordinal);
        Assert.Contains("Q3 2026", text, StringComparison.Ordinal);
        Assert.Contains("ZAR 1,250,000.50", text, StringComparison.Ordinal);
        Assert.Contains("%%EOF", text, StringComparison.Ordinal);
    }
}
