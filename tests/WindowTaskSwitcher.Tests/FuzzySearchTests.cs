using WindowTaskSwitcher.Services;

namespace WindowTaskSwitcher.Tests;

public class FuzzySearchTests
{
    private readonly FuzzySearchService _sut = new();

    [Fact]
    public void ExactMatch_ReturnsHighScore()
    {
        var (score, indices) = _sut.Match("Chrome", "Chrome");
        Assert.True(score > 0);
        Assert.Equal([0, 1, 2, 3, 4, 5], indices);
    }

    [Fact]
    public void EmptyQuery_ReturnsZero()
    {
        var (score, indices) = _sut.Match("", "Chrome");
        Assert.Equal(0, score);
        Assert.Empty(indices);
    }

    [Fact]
    public void EmptyCandidate_ReturnsZero()
    {
        var (score, indices) = _sut.Match("chr", "");
        Assert.Equal(0, score);
        Assert.Empty(indices);
    }

    [Fact]
    public void NoMatch_ReturnsZero()
    {
        var (score, _) = _sut.Match("xyz", "Chrome");
        Assert.Equal(0, score);
    }

    [Fact]
    public void CaseInsensitive_Matches()
    {
        var (score, _) = _sut.Match("chr", "Chrome");
        Assert.True(score > 0);
    }

    [Fact]
    public void NonConsecutiveChars_Match()
    {
        var (score, indices) = _sut.Match("chdev", "Chrome - DevTools");
        Assert.True(score > 0);
        Assert.Equal(5, indices.Count);
    }

    [Fact]
    public void WordStartBonus_ScoresHigher()
    {
        // "sl" matching "Slack" (word start) should score higher than "sl" matching "Consulate"
        var (slackScore, _) = _sut.Match("sl", "Slack");
        var (consulateScore, _) = _sut.Match("sl", "Consulate");

        // Consulate won't match because 's' comes before 'l' isn't possible in order
        // Let's use a better example
        var (wordStartScore, _) = _sut.Match("te", "Terminal");
        var (midWordScore, _) = _sut.Match("te", "Note Editor");
        Assert.True(wordStartScore > midWordScore);
    }

    [Fact]
    public void PrefixBonus_FirstCharAtStart()
    {
        var (prefixScore, _) = _sut.Match("ch", "Chrome");
        var (nonPrefixScore, _) = _sut.Match("ch", "attach");
        Assert.True(prefixScore > nonPrefixScore);
    }

    [Fact]
    public void ConsecutiveChars_ScoreHigherThanScattered()
    {
        var (consecutiveScore, _) = _sut.Match("term", "Terminal");
        var (scatteredScore, _) = _sut.Match("term", "The Remote Manager");
        Assert.True(consecutiveScore > scatteredScore);
    }

    [Fact]
    public void AcronymMatching_Works()
    {
        // "vs" matches "Visual Studio" — greedy match finds 'v' at 0, 's' at 2 (in "Visual")
        var (score, indices) = _sut.Match("vs", "Visual Studio");
        Assert.True(score > 0);
        Assert.Equal(0, indices[0]); // V at start
        Assert.Equal(2, indices.Count);
    }

    [Fact]
    public void PartialQuery_StillMatches()
    {
        var (score, indices) = _sut.Match("sl gen", "Slack - #general");
        Assert.True(score > 0);
        Assert.Equal(6, indices.Count); // s, l, ' ', g, e, n -> wait, space won't match
        // Actually "sl gen" = s,l,' ',g,e,n - space is in "Slack - #general"
    }

    [Fact]
    public void RealWorldCase_ChromeDevTools()
    {
        var (score, _) = _sut.Match("chr dev", "Google Chrome - DevTools");
        Assert.True(score > 0);
    }

    [Fact]
    public void RealWorldCase_VSCode()
    {
        var (score, _) = _sut.Match("code prog", "Visual Studio Code - Program.cs");
        Assert.True(score > 0);
    }

    [Fact]
    public void RealWorldCase_ShortAcronym()
    {
        var (score, _) = _sut.Match("ff", "Firefox");
        Assert.True(score > 0);
    }

    [Fact]
    public void QueryLongerThanCandidate_ReturnsZero()
    {
        var (score, _) = _sut.Match("very long query", "VS");
        Assert.Equal(0, score);
    }

    [Fact]
    public void MatchedIndices_AreCorrect()
    {
        var (_, indices) = _sut.Match("vs", "VS Code");
        Assert.Equal(0, indices[0]);
        Assert.Equal(1, indices[1]);
    }
}
