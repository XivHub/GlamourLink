using System;

namespace GlamourLink.Eorzea;

/// <summary>
/// Splits an Eorzea Collection `dyes` string ("jet-black,none",
/// "dalamud-red") into the up-to-two stain slugs it carries.
/// </summary>
public static class DyeSlugs
{
    public static (string? First, string? Second) Parse(string? dyes)
    {
        if (string.IsNullOrWhiteSpace(dyes))
        {
            return (null, null);
        }

        var parts = dyes.Split(',');
        var first = NormalizePart(parts.Length > 0 ? parts[0] : null);
        var second = NormalizePart(parts.Length > 1 ? parts[1] : null);
        return (first, second);
    }

    public static string ToSheetName(string slug) => slug.Replace('-', ' ').Trim();

    private static string? NormalizePart(string? part)
    {
        if (part is null)
        {
            return null;
        }

        var trimmed = part.Trim();
        if (trimmed.Length == 0 || trimmed.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed;
    }
}
