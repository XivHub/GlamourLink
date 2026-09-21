using System.Text.RegularExpressions;

namespace GlamourLink.Eorzea;

/// <summary>
/// Extracts a glamour id from whatever a user pastes: a full page URL, an
/// `/api/` URL, or a bare id.
/// </summary>
public static partial class EorzeaUrl
{
    [GeneratedRegex(@"glamour/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex GlamourIdRegex();

    public static bool TryParseId(string input, out int id, out string error)
    {
        id = 0;
        error = "";

        var trimmed = input.Trim();
        if (trimmed.Length == 0)
        {
            error = "Paste an Eorzea Collection glamour URL or id.";
            return false;
        }

        if (trimmed.Length > 256)
        {
            error = "That does not look like a glamour URL.";
            return false;
        }

        string digits;
        if (IsAllDigits(trimmed))
        {
            digits = trimmed;
        }
        else
        {
            var match = GlamourIdRegex().Match(trimmed);
            if (!match.Success)
            {
                error = "No glamour id found in that text.";
                return false;
            }

            digits = match.Groups[1].Value;
        }

        if (!int.TryParse(digits, out var parsed) || parsed <= 0)
        {
            error = "Glamour ids start at 1.";
            return false;
        }

        id = parsed;
        return true;
    }

    private static bool IsAllDigits(string s)
    {
        foreach (var c in s)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}
