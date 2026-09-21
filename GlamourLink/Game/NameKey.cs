using System.Text;

namespace GlamourLink.Game;

/// <summary>
/// Normalises an item name to a comparable key. Both the sheet keys and every
/// query string go through this, so an EC name typed with a plain apostrophe
/// matches the game's typographic one, and vice versa.
/// </summary>
public static class NameKey
{
    private const char SoftHyphen = '­';
    private const char LeftSingleQuote = '‘';
    private const char RightSingleQuote = '’';

    public static string Normalize(string name)
    {
        var trimmed = name.Trim();
        var builder = new StringBuilder(trimmed.Length);
        var lastWasSpace = false;

        foreach (var c in trimmed)
        {
            if (c == SoftHyphen)
            {
                continue;
            }

            var mapped = c switch
            {
                LeftSingleQuote or RightSingleQuote => '\'',
                _ => c,
            };

            if (char.IsWhiteSpace(mapped))
            {
                if (lastWasSpace)
                {
                    continue;
                }

                builder.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                builder.Append(mapped);
                lastWasSpace = false;
            }
        }

        return builder.ToString();
    }
}
