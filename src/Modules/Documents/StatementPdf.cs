using System.Globalization;
using System.Text;
using Agentic.Blocks.Core;

namespace Hestia.Documents;

/// <summary>Writes a small, valid one-page PDF statement. Used for invented demo data and the IR "publish" form.</summary>
public static class StatementPdf
{
    /// <summary>Creates the PDF bytes.</summary>
    public static byte[] Create(string investorName, string fund, string period, Money closingBalance, DateTimeOffset asOf)
    {
        var lines = new[]
        {
            ("F2", 18, "Harbourline Lending"),
            ("F1", 12, "Capital account statement"),
            ("F1", 11, $"Investor: {investorName}"),
            ("F1", 11, $"Fund: {fund}"),
            ("F1", 11, $"Period: {period}"),
            ("F1", 11, $"As at: {asOf.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}"),
            ("F2", 12, $"Closing capital balance: {closingBalance.Currency} {closingBalance.Amount.ToString("N2", CultureInfo.InvariantCulture)}"),
            ("F1", 9, "Invented demonstration document. Not a real statement."),
        };
        var text = new StringBuilder("BT\n72 760 Td\n");
        foreach (var (font, size, line) in lines)
        {
            text.Append(CultureInfo.InvariantCulture, $"/{font} {size} Tf\n({Escape(line)}) Tj\n0 -{size + 12} Td\n");
        }

        text.Append("ET\n");
        var stream = text.ToString();
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>",
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        pdf.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string Escape(string value) =>
        new(value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal)
            .Select(c => c is >= ' ' and <= '~' ? c : '?').ToArray());
}
