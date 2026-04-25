using System.Globalization;
using System.Text;

namespace FastTools.App.Infrastructure;

public static class SearchMatcher
{
    public static double Score(string query, params string?[] candidates)
    {
        var normalizedQuery = Normalize(query);
        return ScoreNormalized(normalizedQuery, candidates.Select(Normalize).ToArray());
    }

    public static double ScoreNormalized(string normalizedQuery, params string?[] normalizedCandidates)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return 0;
        }

        var best = 0d;
        foreach (var normalizedCandidate in normalizedCandidates)
        {
            if (string.IsNullOrWhiteSpace(normalizedCandidate))
            {
                continue;
            }

            if (normalizedCandidate.Length == 0)
            {
                continue;
            }

            if (normalizedCandidate.Equals(normalizedQuery, StringComparison.Ordinal))
            {
                best = Math.Max(best, 200);
                continue;
            }

            if (normalizedCandidate.StartsWith(normalizedQuery, StringComparison.Ordinal))
            {
                best = Math.Max(best, 160 - (normalizedCandidate.Length - normalizedQuery.Length) * 0.2);
                continue;
            }

            var containsIndex = normalizedCandidate.IndexOf(normalizedQuery, StringComparison.Ordinal);
            if (containsIndex >= 0)
            {
                best = Math.Max(best, 130 - containsIndex * 2);
                continue;
            }

            if (IsSubsequence(normalizedQuery, normalizedCandidate, out var compactness))
            {
                best = Math.Max(best, 80 - compactness);
            }
        }

        return best;
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static bool IsSubsequence(string needle, string haystack, out int compactness)
    {
        compactness = int.MaxValue;
        var firstMatch = -1;
        var lastMatch = -1;
        var index = 0;

        for (var i = 0; i < haystack.Length && index < needle.Length; i++)
        {
            if (haystack[i] != needle[index])
            {
                continue;
            }

            firstMatch = firstMatch == -1 ? i : firstMatch;
            lastMatch = i;
            index++;
        }

        if (index != needle.Length)
        {
            return false;
        }

        compactness = lastMatch - firstMatch - needle.Length;
        return true;
    }
}
