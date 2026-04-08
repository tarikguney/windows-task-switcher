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

        // Quick check: does the candidate contain all query chars in order?
        if (!CanMatch(queryLower, candidateLower))
            return (0, []);

        // Try matching from word-start positions and first occurrence only.
        // This prevents the greedy algorithm from locking onto early characters
        // and missing a much better match later (e.g., "common" matching the 'c' in
        // "Microsoft" instead of the word "Common").
        int bestScore = 0;
        List<int> bestIndices = [];

        for (int start = 0; start < candidateLower.Length; start++)
        {
            if (candidateLower[start] != queryLower[0])
                continue;

            // Only try: first occurrence, word starts, and camelCase transitions
            bool isFirst = start == 0;
            bool isWordStart = start > 0 && IsWordSeparator(candidate[start - 1]);
            bool isCamelCase = start > 0 && char.IsUpper(candidate[start]) && char.IsLower(candidate[start - 1]);
            bool isFirstOccurrence = candidateLower.IndexOf(queryLower[0]) == start;

            if (!isFirst && !isWordStart && !isCamelCase && !isFirstOccurrence)
                continue;

            var (score, indices) = ScoreFromPosition(queryLower, candidateLower, candidate, start);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndices = indices;
            }
        }

        return (bestScore, bestIndices);
    }

    private static (int Score, List<int> MatchedIndices) ScoreFromPosition(
        string queryLower, string candidateLower, string candidateOriginal, int startPos)
    {
        var matchedIndices = new List<int>();
        int score = 0;
        int queryIndex = 0;
        int lastMatchIndex = -1;

        for (int i = startPos; i < candidateLower.Length && queryIndex < queryLower.Length; i++)
        {
            if (candidateLower[i] == queryLower[queryIndex])
            {
                matchedIndices.Add(i);
                score += BaseMatch;

                // Consecutive match bonus
                if (lastMatchIndex >= 0 && i == lastMatchIndex + 1)
                    score += ConsecutiveBonus;

                // Word start bonus (first char or after separator)
                if (i == 0 || IsWordSeparator(candidateOriginal[i - 1]))
                    score += WordStartBonus;

                // CamelCase bonus
                if (i > 0 && char.IsUpper(candidateOriginal[i]) && char.IsLower(candidateOriginal[i - 1]))
                    score += CamelCaseBonus;

                // Prefix bonus (match starts at very beginning of candidate)
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

    private static bool CanMatch(string queryLower, string candidateLower)
    {
        int qi = 0;
        for (int i = 0; i < candidateLower.Length && qi < queryLower.Length; i++)
        {
            if (candidateLower[i] == queryLower[qi])
                qi++;
        }
        return qi == queryLower.Length;
    }

    private static bool IsWordSeparator(char c) =>
        c is ' ' or '-' or '_' or '.' or '/' or '\\' or ':' or '(' or ')' or '[' or ']';
}
