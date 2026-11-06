using System.Text.RegularExpressions;
using DynamicWebAutomation.Models;

namespace DynamicWebAutomation.Detection;

/// <summary>
/// Ranks visible controls using plain text and attributes, so locator decisions
/// are deterministic and easy to unit-test without Selenium.
/// </summary>
public sealed class CandidateScorer
{
    private static readonly (string Name, int Weight, Func<CandidateEvidence, IReadOnlyList<string>> Values)[] StrongEvidence =
    [
        ("label-for text", 100, candidate => candidate.LabelForTexts),
        ("wrapping label text", 95, candidate => candidate.WrappingLabelTexts),
        ("aria-labelledby text", 90, candidate => candidate.AriaLabelledByTexts),
        ("aria-label text", 85, candidate => candidate.AriaLabels),
        ("nearest group text", 80, candidate => candidate.NearbyTexts)
    ];

    /// <summary>
    /// Filters by visibility and control type, then accepts a candidate only
    /// when it meets the minimum score and clearly leads the runner-up.
    /// </summary>
    public CandidateSelection Select(
        IEnumerable<CandidateEvidence> candidates,
        string targetText,
        string expectedControlType,
        int minimumScore,
        int minimumLead)
    {
        CandidateEvidence[] eligible = candidates
            .Where(candidate => candidate.IsVisible &&
                                string.Equals(candidate.ControlType, expectedControlType, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        ScoredCandidate[] ranked = eligible
            .Select(candidate => Score(candidate, targetText))
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Candidate.Description, StringComparer.Ordinal)
            .ToArray();

        if (ranked.Length == 0)
        {
            return new CandidateSelection(SelectionStatus.NotFound, null, null, ranked,
                $"No visible {expectedControlType} candidates were found.");
        }

        ScoredCandidate best = ranked[0];
        ScoredCandidate? runnerUp = ranked.Length > 1 ? ranked[1] : null;

        int exactLabelCount = ranked.Count(candidate => HasExactStrongEvidence(candidate.Candidate, targetText));
        if (exactLabelCount > 1)
        {
            return new CandidateSelection(SelectionStatus.Ambiguous, best, runnerUp, ranked,
                $"The exact visible label '{Normalize(targetText)}' appears on more than one candidate.");
        }

        if (best.Score < minimumScore)
        {
            return new CandidateSelection(SelectionStatus.NotFound, best, runnerUp, ranked,
                $"Best score {best.Score} is below the configured minimum {minimumScore}.");
        }

        int lead = best.Score - (runnerUp?.Score ?? 0);
        if (runnerUp is not null && (lead == 0 || lead < minimumLead))
        {
            return new CandidateSelection(SelectionStatus.Ambiguous, best, runnerUp, ranked,
                lead == 0
                    ? "The top candidates have equal scores."
                    : $"Lead {lead} is below the configured minimum margin {minimumLead}.");
        }

        return new CandidateSelection(SelectionStatus.Selected, best, runnerUp, ranked,
            $"Chosen because of {best.Reason}.");
    }

    /// <summary>
    /// Normalizes text in one place so labels with extra whitespace or case
    /// differences still compare consistently.
    /// </summary>
    public static string Normalize(string? text) =>
        Regex.Replace(text ?? "", @"\s+", " ").Trim().ToUpperInvariant();

    private static ScoredCandidate Score(CandidateEvidence candidate, string targetText)
    {
        string target = Normalize(targetText);
        foreach ((string name, int weight, Func<CandidateEvidence, IReadOnlyList<string>> values) in StrongEvidence)
        {
            IReadOnlyList<string> texts = values(candidate);
            if (texts.Any(text => Normalize(text) == target))
            {
                return new ScoredCandidate(candidate, weight, $"exact {name}");
            }
        }

        foreach ((string name, int weight, Func<CandidateEvidence, IReadOnlyList<string>> values) in StrongEvidence)
        {
            if (values(candidate).Any(text => Normalize(text).Contains(target, StringComparison.Ordinal)))
            {
                return new ScoredCandidate(candidate, weight / 2, $"partial {name}");
            }
        }

        if (candidate.WeakHints.Any(text => Normalize(text) == target))
        {
            return new ScoredCandidate(candidate, 20, "exact weak attribute hint");
        }

        if (candidate.WeakHints.Any(text => Normalize(text).Contains(target, StringComparison.Ordinal)))
        {
            return new ScoredCandidate(candidate, 10, "partial weak attribute hint");
        }

        return new ScoredCandidate(candidate, 0, "no matching label evidence");
    }

    private static bool HasExactStrongEvidence(CandidateEvidence candidate, string targetText)
    {
        string target = Normalize(targetText);
        return StrongEvidence.Any(evidence => evidence.Values(candidate)
            .Any(text => Normalize(text) == target));
    }
}
