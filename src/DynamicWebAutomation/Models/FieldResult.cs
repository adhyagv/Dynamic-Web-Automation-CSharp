namespace DynamicWebAutomation.Models;

public enum FieldStatus
{
    /// <summary>
    /// The expected state was read back from the page.
    /// </summary>
    Pass,

    /// <summary>
    /// The field could not be found or did not match its expected state.
    /// </summary>
    Fail,

    /// <summary>
    /// Multiple candidates prevented a safe choice.
    /// </summary>
    Ambiguous,

    /// <summary>
    /// The requested State option was absent, so it was not verified.
    /// </summary>
    SkippedOptionNotAvailable
}

/// <summary>
/// Stores a field outcome and the evidence needed to explain its locator choice.
/// </summary>
public sealed class FieldResult
{
    public required string FieldName { get; init; }
    public required FieldStatus Status { get; init; }
    public required string Message { get; init; }
    public string? ElementDescription { get; init; }
    public int? ChosenScore { get; init; }
    public int? RunnerUpScore { get; init; }
    public string? ChoiceReason { get; init; }
    public IReadOnlyList<string> AvailableOptions { get; init; } = [];
    public string? ScreenshotPath { get; set; }
    public string? ScreenshotError { get; set; }
}
