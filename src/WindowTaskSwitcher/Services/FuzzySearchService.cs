namespace WindowTaskSwitcher.Services;

public sealed class FuzzySearchService
{
    private const int BaseMatch = 1;
    private const int ConsecutiveBonus = 5;
    private const int WordStartBonus = 10;
    private const int CamelCaseBonus = 8;
    private const int PrefixBonus = 15;
    private const int GapPenalty = -1;

    public (int Score, List<int> MatchedIndices) Match(string query, string candidate)
    {
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(candidate))
            return (0, []);

        string queryLower = query.ToLowerInvariant();
        string candidateLower = candidate.ToLowerInvariant();

        var matchedIndices = new List<int>();
        int score = 0;
        int queryIndex = 0;
        int lastMatchIndex = -1;

        for (int i = 0; i < candidateLower.Length && queryIndex < queryLower.Length; i++)
        {
            if (candidateLower[i] == queryLower[queryIndex])
            {
                matchedIndices.Add(i);
                score += BaseMatch;

                // Consecutive match bonus
                if (lastMatchIndex >= 0 && i == lastMatchIndex + 1)
                    score += ConsecutiveBonus;

                // Word start bonus (first char or after separator)
                if (i == 0 || IsWordSeparator(candidate[i - 1]))
                    score += WordStartBonus;

                // CamelCase bonus
                if (i > 0 && char.IsUpper(candidate[i]) && char.IsLower(candidate[i - 1]))
                    score += CamelCaseBonus;

                // Prefix bonus
                if (i == 0 && queryIndex == 0)
                    score += PrefixBonus;

                // Gap penalty (unmatched characters between matches)
                if (lastMatchIndex >= 0)
                {
                    int gap = i - lastMatchIndex - 1;
                    score += gap * GapPenalty;
                }

                lastMatchIndex = i;
                queryIndex++;
            }
        }

        // All query characters must match
        if (queryIndex < queryLower.Length)
            return (0, []);

        return (score, matchedIndices);
    }

    private static bool IsWordSeparator(char c) =>
        c is ' ' or '-' or '_' or '.' or '/' or '\\' or ':' or '(' or ')' or '[' or ']';
}
