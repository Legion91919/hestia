using System.Text.RegularExpressions;
using Agentic.Blocks.Core;

namespace Hestia.Documents;

/// <summary>Domain rules for publishing a document.</summary>
public static partial class DocumentRules
{
    /// <summary>Allowed content types.</summary>
    public static readonly IReadOnlySet<string> ContentTypes = new HashSet<string>(StringComparer.Ordinal) { "application/pdf", "text/plain" };

    /// <summary>Checks a publish command.</summary>
    public static Result Validate(PublishDocument command, long maxBytes)
    {
        ArgumentNullException.ThrowIfNull(command);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(command.Title) || command.Title.Length > 120) errors["title"] = ["Title is required, up to 120 characters."];
        if (string.IsNullOrWhiteSpace(command.Fund) || command.Fund.Length > 120) errors["fund"] = ["Fund is required, up to 120 characters."];
        if (command.Period is null || !Quarter().IsMatch(command.Period)) errors["period"] = ["Period is a quarter such as 'Q3 2026'."];
        if (command.FileName is null || !SafeFileName().IsMatch(command.FileName)) errors["fileName"] = ["File name has letters, digits, dots, dashes or underscores, and no path."];
        if (command.Content is null || command.Content.Length == 0) errors["content"] = ["The file is empty."];
        else if (command.Content.Length > maxBytes) errors["content"] = [$"The file is larger than {maxBytes / 1024} KB."];
        if (!ContentTypes.Contains(command.ContentType ?? string.Empty)) errors["contentType"] = ["Only PDF and plain text are accepted."];
        else if (command.ContentType == "application/pdf" && command.Content is { Length: > 0 } && !IsPdf(command.Content)) errors["content"] = ["The file is not a PDF."];
        return errors.Count == 0 ? Result.Success() : Error.Validation("document.invalid", "The document is not valid.", errors);
    }

    private static bool IsPdf(byte[] content) => content.Length >= 5 && content.AsSpan(0, 5).SequenceEqual("%PDF-"u8);

    [GeneratedRegex("^Q[1-4] 20[0-9]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex Quarter();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeFileName();
}
