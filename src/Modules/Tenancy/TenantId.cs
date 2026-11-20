using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Hestia.Tenancy;

/// <summary>
/// One investor entity (a tenant). A lower-case slug, so it is safe in claims, URLs and file paths.
/// </summary>
public readonly partial record struct TenantId
{
    private TenantId(string value) => Value = value;

    /// <summary>The slug, for example <c>kestrel-trust</c>.</summary>
    public string Value { get; }

    /// <summary>Parses a slug of 2-63 characters: a-z, 0-9 and hyphens, starting with a letter or digit.</summary>
    public static bool TryParse([NotNullWhen(true)] string? value, out TenantId tenant)
    {
        tenant = default;
        if (value is null || !Slug().IsMatch(value)) return false;
        tenant = new TenantId(value);
        return true;
    }

    /// <summary>Parses or throws <see cref="FormatException"/>.</summary>
    public static TenantId Parse(string value) =>
        TryParse(value, out var tenant) ? tenant : throw new FormatException("A tenant id is a lower-case slug.");

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,62}$", RegexOptions.CultureInvariant)]
    private static partial Regex Slug();
}
