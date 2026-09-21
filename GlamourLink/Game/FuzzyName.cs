using System;
using System.Collections.Generic;

namespace GlamourLink.Game;

/// <summary>
/// Bounded Levenshtein matching for Eorzea Collection's hand-entered names
/// against the game's own. A last resort behind an exact lookup: EC's names
/// carry roughly one typo in eighty (see GlamourLink-ec-api-notes.md), so a
/// tight distance cap and a uniqueness requirement keep a wrong guess rare
/// and, when it happens, labelled.
/// </summary>
public static class FuzzyName
{
    /// <summary>
    /// Levenshtein edit distance between two already-normalised, lowercase
    /// strings, capped at <paramref name="max"/>: once every cell in a row
    /// exceeds <paramref name="max"/>, no cell reachable from it can produce
    /// a smaller final distance, so the row (and the whole comparison) can
    /// stop early.
    /// </summary>
    public static int Distance(string a, string b, int max)
    {
        if (a.Length == 0)
        {
            return Math.Min(b.Length, max + 1);
        }

        if (b.Length == 0)
        {
            return Math.Min(a.Length, max + 1);
        }

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowMin = current[0];
            var charA = a[i - 1];

            for (var j = 1; j <= b.Length; j++)
            {
                var cost = charA == b[j - 1] ? 0 : 1;
                var deletion = previous[j] + 1;
                var insertion = current[j - 1] + 1;
                var substitution = previous[j - 1] + cost;
                var value = Math.Min(deletion, Math.Min(insertion, substitution));
                current[j] = value;
                if (value < rowMin)
                {
                    rowMin = value;
                }
            }

            if (rowMin > max)
            {
                return max + 1;
            }

            (previous, current) = (current, previous);
        }

        return Math.Min(previous[b.Length], max + 1);
    }

    /// <summary>
    /// Finds the single closest candidate within <paramref name="maxDistance"/>.
    /// Returns false when nothing is within range, or when two or more
    /// candidates tie for the best distance: an ambiguous near-match is a
    /// miss, not a coin flip.
    /// </summary>
    public static bool TryBestMatch(
        IReadOnlyList<(uint Id, string Name)> candidates,
        string query,
        int maxDistance,
        out uint id,
        out string name)
    {
        id = 0;
        name = "";

        var bestDistance = maxDistance + 1;
        var bestCount = 0;
        uint bestId = 0;
        var bestName = "";

        foreach (var candidate in candidates)
        {
            if (Math.Abs(candidate.Name.Length - query.Length) > maxDistance)
            {
                continue;
            }

            var distance = Distance(query, candidate.Name, maxDistance);
            if (distance > maxDistance)
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCount = 1;
                bestId = candidate.Id;
                bestName = candidate.Name;
            }
            else if (distance == bestDistance)
            {
                bestCount++;
            }
        }

        if (bestDistance > maxDistance || bestCount != 1)
        {
            return false;
        }

        id = bestId;
        name = bestName;
        return true;
    }
}
