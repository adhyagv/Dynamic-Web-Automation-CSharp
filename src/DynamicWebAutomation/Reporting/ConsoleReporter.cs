using DynamicWebAutomation.Models;

namespace DynamicWebAutomation.Reporting;

/// <summary>
/// Prints field outcomes and a summary that never describes skipped work as
/// verified.
/// </summary>
public static class ConsoleReporter
{
    public static void PrintResult(FieldResult result)
    {
        string status = result.Status switch
        {
            FieldStatus.Pass => "PASS",
            FieldStatus.Fail => "FAIL",
            FieldStatus.Ambiguous => "AMBIGUOUS",
            FieldStatus.SkippedOptionNotAvailable => "SKIPPED_OPTION_NOT_AVAILABLE",
            FieldStatus.NotRun => "NOT_RUN",
            _ => throw new ArgumentOutOfRangeException(nameof(result.Status))
        };

        Console.WriteLine($"[{status}] {result.FieldName}: {result.Message}");
        if (result.FieldName == "Gender" && result.GroupName is not null)
        {
            Console.WriteLine(
                $"[Gender details] group='{result.GroupName}', options={result.RadioOptionCount}, " +
                $"selected text='{result.SelectedOptionText ?? "(none)"}', checked={result.ReadBackChecked?.ToString() ?? "(not read)"}.");
        }
    }

    public static void PrintSummary(RunSummary summary)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"Counts: {summary.Passed} passed, {summary.Failed} failed, {summary.Ambiguous} ambiguous, {summary.Skipped} skipped, {summary.NotRun} not run.");
        Console.WriteLine(summary.FinalMessage);
    }
}
