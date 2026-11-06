using DynamicWebAutomation.Models;

namespace DynamicWebAutomation.Tests;

/// <summary>
/// Verifies skipped fields are reported as unverified, not successful passes.
/// </summary>
public sealed class RunSummaryTests
{
    [Fact]
    public void SkippedStateUsesPassedWithSkippedWordingAndConfigurableExitCode()
    {
        FieldResult skippedState = new()
        {
            FieldName = "State",
            Status = FieldStatus.SkippedOptionNotAvailable,
            Message = "Karnataka is not available."
        };

        RunSummary permissive = new([skippedState], strictStateOption: false);
        Assert.Equal("PASSED with 1 skipped", permissive.FinalMessage);
        Assert.Equal(0, permissive.ExitCode);

        RunSummary strict = new([skippedState], strictStateOption: true);
        Assert.Equal("FAILED with 1 skipped", strict.FinalMessage);
        Assert.Equal(1, strict.ExitCode);
    }
}
