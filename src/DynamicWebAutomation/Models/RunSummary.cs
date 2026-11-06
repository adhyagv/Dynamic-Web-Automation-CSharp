namespace DynamicWebAutomation.Models;

/// <summary>
/// Keeps outcome counts distinct; skipped fields are explicitly not verified.
/// </summary>
public sealed class RunSummary(IReadOnlyList<FieldResult> results, bool strictStateOption)
{
    public int Passed => results.Count(result => result.Status == FieldStatus.Pass);
    public int Failed => results.Count(result => result.Status == FieldStatus.Fail);
    public int Ambiguous => results.Count(result => result.Status == FieldStatus.Ambiguous);
    public int Skipped => results.Count(result => result.Status == FieldStatus.SkippedOptionNotAvailable);
    public int NotRun => results.Count(result => result.Status == FieldStatus.NotRun);
    public bool HasFailure => Failed > 0 || Ambiguous > 0 || (strictStateOption && Skipped > 0);
    public int ExitCode => HasFailure ? 1 : 0;

    public string FinalMessage =>
        $"{(HasFailure ? "FAILED" : "PASSED")} with {Skipped} skipped" +
        (NotRun > 0 ? $" and {NotRun} not run" : "");
}
