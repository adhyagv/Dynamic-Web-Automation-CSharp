using DynamicWebAutomation.Detection;
using DynamicWebAutomation.Models;

namespace DynamicWebAutomation.Tests;

/// <summary>
/// Tests candidate scoring with plain data, without launching a browser.
/// </summary>
public sealed class CandidateScorerTests
{
    private readonly CandidateScorer scorer = new();

    [Fact]
    public void ExactLabelBeatsPartialLabel()
    {
        CandidateEvidence exact = Candidate(labelFor: ["First Name"]);
        CandidateEvidence partial = Candidate(nearby: ["First Name (required)"]);

        CandidateSelection result = scorer.Select([partial, exact], "First Name", "text", 60, 15);

        Assert.Equal(SelectionStatus.Selected, result.Status);
        Assert.Same(exact, result.Chosen!.Candidate);
        Assert.Equal(100, result.Chosen.Score);
        Assert.Equal(40, result.RunnerUp!.Score);
    }

    [Fact]
    public void CandidateBelowMinimumScoreIsRejected()
    {
        CandidateSelection result = scorer.Select(
            [Candidate(weakHints: ["First Name"])], "First Name", "text", 60, 15);

        Assert.Equal(SelectionStatus.NotFound, result.Status);
        Assert.Equal(20, result.Chosen!.Score);
    }

    [Fact]
    public void CandidatesWithCloseScoresAreAmbiguous()
    {
        CandidateSelection result = scorer.Select(
            [Candidate(labelFor: ["First Name"]), Candidate(nearby: ["First Name (required)"])],
            "First Name",
            "text",
            60,
            65);

        Assert.Equal(SelectionStatus.Ambiguous, result.Status);
        Assert.Equal(100, result.Chosen!.Score);
        Assert.Equal(40, result.RunnerUp!.Score);
        Assert.Contains("below the configured minimum margin", result.Message);
    }

    [Fact]
    public void MatchingNormalizesWhitespaceAndCase()
    {
        CandidateSelection result = scorer.Select(
            [Candidate(labelFor: ["  first\t   NAME  "])],
            " First  Name ",
            "text",
            60,
            15);

        Assert.Equal(SelectionStatus.Selected, result.Status);
        Assert.Equal(100, result.Chosen!.Score);
    }

    [Fact]
    public void CandidateOfWrongControlTypeIsExcluded()
    {
        CandidateSelection result = scorer.Select(
            [Candidate(type: "select", labelFor: ["First Name"])],
            "First Name",
            "text",
            60,
            15);

        Assert.Equal(SelectionStatus.NotFound, result.Status);
        Assert.Empty(result.RankedCandidates);
    }

    [Fact]
    public void EqualScoresAreAmbiguousEvenWhenMarginIsZero()
    {
        CandidateSelection result = scorer.Select(
            [Candidate(weakHints: ["First Name"]), Candidate(weakHints: ["First Name"])],
            "First Name",
            "text",
            10,
            0);

        Assert.Equal(SelectionStatus.Ambiguous, result.Status);
        Assert.Equal(result.Chosen!.Score, result.RunnerUp!.Score);
    }

    [Fact]
    public void DuplicateExactLabelsAreAmbiguous()
    {
        CandidateSelection result = scorer.Select(
            [Candidate(labelFor: ["First Name"]), Candidate(labelFor: ["First Name"])],
            "First Name",
            "text",
            60,
            15);

        Assert.Equal(SelectionStatus.Ambiguous, result.Status);
        Assert.Contains("more than one", result.Message);
    }

    private static CandidateEvidence Candidate(
        string type = "text",
        IReadOnlyList<string>? labelFor = null,
        IReadOnlyList<string>? wrapping = null,
        IReadOnlyList<string>? ariaLabelledBy = null,
        IReadOnlyList<string>? ariaLabel = null,
        IReadOnlyList<string>? nearby = null,
        IReadOnlyList<string>? weakHints = null) =>
        new(
            $"<{type}> test candidate",
            type,
            true,
            labelFor ?? [],
            wrapping ?? [],
            ariaLabelledBy ?? [],
            ariaLabel ?? [],
            nearby ?? [],
            weakHints ?? []);
}
