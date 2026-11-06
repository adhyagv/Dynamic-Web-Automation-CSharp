namespace DynamicWebAutomation.Models;

/// <summary>
/// Plain page evidence kept separate from WebDriver so ranking can be tested
/// without opening a browser.
/// </summary>
public sealed record CandidateEvidence(
    string Description,
    string ControlType,
    bool IsVisible,
    IReadOnlyList<string> LabelForTexts,
    IReadOnlyList<string> WrappingLabelTexts,
    IReadOnlyList<string> AriaLabelledByTexts,
    IReadOnlyList<string> AriaLabels,
    IReadOnlyList<string> NearbyTexts,
    IReadOnlyList<string> WeakHints);

public enum SelectionStatus
{
    /// <summary>
    /// A candidate passed the minimum score and runner-up margin.
    /// </summary>
    Selected,

    /// <summary>
    /// No eligible candidate reached the configured score threshold.
    /// </summary>
    NotFound,

    /// <summary>
    /// The evidence did not distinguish one candidate safely from another.
    /// </summary>
    Ambiguous
}

/// <summary>
/// Holds one candidate's numeric score and the strongest reason supporting it.
/// </summary>
public sealed record ScoredCandidate(CandidateEvidence Candidate, int Score, string Reason);

/// <summary>
/// Returns the selection decision and the two strongest candidates for reporting.
/// </summary>
public sealed record CandidateSelection(
    SelectionStatus Status,
    ScoredCandidate? Chosen,
    ScoredCandidate? RunnerUp,
    IReadOnlyList<ScoredCandidate> RankedCandidates,
    string Message);
