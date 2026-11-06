using DynamicWebAutomation.Configuration;
using DynamicWebAutomation.Core;
using DynamicWebAutomation.Detection;
using DynamicWebAutomation.Models;
using DynamicWebAutomation.Reporting;
using DynamicWebAutomation.Validation;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace DynamicWebAutomation.Automation;

/// <summary>
/// Runs only the three requested fields and treats read-back, not an action,
/// as the condition for PASS.
/// </summary>
public sealed class PracticeFormAutomation
{
    private const string FirstNameLabel = "First Name";
    private const string GenderLabel = "Gender";
    private const string StateLabel = "State";

    private readonly IWebDriver driver;
    private readonly AutomationSettings settings;
    private readonly CandidateCollector collector;
    private readonly CandidateScorer scorer = new();
    private readonly ReportWriter reportWriter;
    private readonly WebDriverWait elementWait;
    private readonly WebDriverWait optionsWait;

    public PracticeFormAutomation(IWebDriver driver, AutomationSettings settings, ReportWriter reportWriter)
    {
        this.driver = driver;
        this.settings = settings;
        this.reportWriter = reportWriter;
        collector = new CandidateCollector(driver);
        elementWait = new WebDriverWait(driver, TimeSpan.FromSeconds(settings.Timeouts.ElementSeconds));
        optionsWait = new WebDriverWait(driver, TimeSpan.FromSeconds(settings.Timeouts.OptionsSeconds));
    }

    public IReadOnlyList<FieldResult> Run()
    {
        FieldResult firstName = RunWithRetry(FirstNameLabel, RunFirstName);
        FieldResult gender = RunWithRetry(GenderLabel, RunGender);
        FieldResult state = RunWithRetry(StateLabel, RunState);
        return [firstName, gender, state];
    }

    private FieldResult RunWithRetry(string fieldName, Func<FieldResult> operation)
    {
        try
        {
            return RetryPolicy.Execute(operation, settings.RetryCount);
        }
        catch (WebDriverException exception)
        {
            return Complete(new FieldResult
            {
                FieldName = fieldName,
                Status = FieldStatus.Fail,
                Message = $"WebDriver could not complete the field: {exception.Message}"
            });
        }
    }

    private FieldResult RunFirstName()
    {
        Console.WriteLine($"Looking for {FirstNameLabel} text input...");
        IReadOnlyList<PageCandidate> candidates = WaitForCandidates(() => collector.CollectFields("text"));
        CandidateSelection selection = Select(candidates, FirstNameLabel, "text");
        LogSelection(FirstNameLabel, selection);
        if (selection.Status != SelectionStatus.Selected)
        {
            return Complete(SelectionFailure(FirstNameLabel, selection));
        }

        PageCandidate candidate = FindChosen(candidates, selection);
        candidate.Element.Clear();
        candidate.Element.SendKeys(settings.Fields.FirstName);
        string actual = candidate.Element.GetAttribute("value") ?? "";
        bool passed = FieldValidator.HasExactTextValue(candidate.Element, settings.Fields.FirstName);

        return Complete(CreateResult(
            FirstNameLabel,
            passed ? FieldStatus.Pass : FieldStatus.Fail,
            passed
                ? $"Typed value was read back exactly as '{actual}'."
                : $"Expected '{settings.Fields.FirstName}' but read back '{actual}'.",
            candidate,
            selection));
    }

    private FieldResult RunGender()
    {
        Console.WriteLine($"Looking for {GenderLabel} radio group...");
        IReadOnlyList<PageCandidate> groups = WaitForCandidates(() => collector.CollectRadioGroups());
        CandidateSelection selection = Select(groups, GenderLabel, "radio-group");
        LogSelection(GenderLabel, selection);
        if (selection.Status != SelectionStatus.Selected)
        {
            return Complete(SelectionFailure(GenderLabel, selection));
        }

        PageCandidate group = FindChosen(groups, selection);
        string expected = CandidateScorer.Normalize(settings.Fields.Gender);
        List<IWebElement> matchingOptions = group.RadioMembers
            .Where(radio => collector.GetRadioOptionTexts(radio)
                .Any(text => CandidateScorer.Normalize(text) == expected))
            .ToList();

        if (matchingOptions.Count == 0)
        {
            return Complete(CreateResult(
                GenderLabel,
                FieldStatus.Fail,
                $"The '{settings.Fields.Gender}' radio option text was not found in the selected group.",
                group,
                selection));
        }

        if (matchingOptions.Count > 1)
        {
            return Complete(CreateResult(
                GenderLabel,
                FieldStatus.Ambiguous,
                $"More than one radio option has the exact text '{settings.Fields.Gender}'.",
                group,
                selection));
        }

        IWebElement option = matchingOptions[0];
        string optionDescription = collector.GetRadioOptionTexts(option).First(text =>
            CandidateScorer.Normalize(text) == expected);
        ((IJavaScriptExecutor)driver).ExecuteScript(
            "arguments[0].scrollIntoView({ block: 'center', inline: 'nearest' });",
            option);
        option.Click();
        bool passed = FieldValidator.IsSelected(option);

        return Complete(CreateResult(
            GenderLabel,
            passed ? FieldStatus.Pass : FieldStatus.Fail,
            passed
                ? $"Radio option text '{optionDescription}' is selected."
                : $"Radio option text '{optionDescription}' was clicked but is not selected.",
            group with
            {
                Evidence = group.Evidence with
                {
                    Description = $"{group.Evidence.Description}; option text '{optionDescription}'"
                },
                Element = option
            },
            selection));
    }

    private FieldResult RunState()
    {
        Console.WriteLine($"Looking for {StateLabel} select...");
        IReadOnlyList<PageCandidate> candidates = WaitForCandidates(() => collector.CollectFields("select"));
        CandidateSelection selection = Select(candidates, StateLabel, "select");
        LogSelection(StateLabel, selection);
        if (selection.Status != SelectionStatus.Selected)
        {
            return Complete(SelectionFailure(StateLabel, selection));
        }

        PageCandidate candidate = FindChosen(candidates, selection);
        SelectElement select = new(candidate.Element);
        string expected = CandidateScorer.Normalize(settings.Fields.State);
        optionsWait.Until(_ =>
            candidate.Element.Displayed &&
            (select.Options.Count > 1 ||
             select.Options.Any(option => CandidateScorer.Normalize(option.Text) == expected)));

        string[] availableOptions = select.Options
            .Select(option => (option.Text ?? "").Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
        string[] exactOptions = availableOptions
            .Where(text => CandidateScorer.Normalize(text) == expected)
            .ToArray();

        if (exactOptions.Length == 0)
        {
            return Complete(CreateResult(
                StateLabel,
                FieldStatus.SkippedOptionNotAvailable,
                $"Option '{settings.Fields.State}' is not available; this field was not verified.",
                candidate,
                selection,
                availableOptions));
        }

        if (exactOptions.Length > 1)
        {
            return Complete(CreateResult(
                StateLabel,
                FieldStatus.Ambiguous,
                $"Option text '{settings.Fields.State}' appears more than once in the State select.",
                candidate,
                selection,
                availableOptions));
        }

        select.SelectByText(exactOptions[0]);
        bool passed = FieldValidator.HasSelectedOptionText(select, settings.Fields.State);
        string selectedText = select.SelectedOption.Text.Trim();

        return Complete(CreateResult(
            StateLabel,
            passed ? FieldStatus.Pass : FieldStatus.Fail,
            passed
                ? $"Selected option was read back as '{selectedText}'."
                : $"Expected selected option '{settings.Fields.State}' but read back '{selectedText}'.",
            candidate,
            selection,
            availableOptions));
    }

    private CandidateSelection Select(
        IReadOnlyList<PageCandidate> candidates,
        string targetText,
        string controlType) =>
        scorer.Select(
            candidates.Select(candidate => candidate.Evidence),
            targetText,
            controlType,
            settings.Scoring.MinimumScore,
            settings.Scoring.MinimumLead);

    private IReadOnlyList<PageCandidate> WaitForCandidates(Func<IReadOnlyList<PageCandidate>> collect) =>
        elementWait.Until<IReadOnlyList<PageCandidate>>(_ =>
        {
            IReadOnlyList<PageCandidate> candidates = collect();
            return candidates.Count > 0 ? candidates : null;
        })!;

    private static PageCandidate FindChosen(IReadOnlyList<PageCandidate> candidates, CandidateSelection selection) =>
        candidates.First(candidate => ReferenceEquals(candidate.Evidence, selection.Chosen!.Candidate));

    private static void LogSelection(string fieldName, CandidateSelection selection)
    {
        if (selection.Chosen is null)
        {
            Console.WriteLine($"[{fieldName}] No candidate to score: {selection.Message}");
            return;
        }

        int runnerUp = selection.RunnerUp?.Score ?? 0;
        string outcome = selection.Status switch
        {
            SelectionStatus.Selected => "chosen",
            SelectionStatus.Ambiguous => "AMBIGUOUS; no candidate chosen",
            _ => "rejected"
        };
        Console.WriteLine(
            $"[{fieldName}] best match score {selection.Chosen.Score}, runner-up {runnerUp} -> {outcome}. " +
            $"Reason: {selection.Message} Candidate: {selection.Chosen.Candidate.Description}");
    }

    private static FieldResult SelectionFailure(string fieldName, CandidateSelection selection)
    {
        ScoredCandidate? top = selection.Chosen;
        return new FieldResult
        {
            FieldName = fieldName,
            Status = selection.Status == SelectionStatus.Ambiguous ? FieldStatus.Ambiguous : FieldStatus.Fail,
            Message = selection.Message,
            ElementDescription = top?.Candidate.Description,
            ChosenScore = top?.Score,
            RunnerUpScore = selection.RunnerUp?.Score,
            ChoiceReason = top?.Reason
        };
    }

    private static FieldResult CreateResult(
        string fieldName,
        FieldStatus status,
        string message,
        PageCandidate candidate,
        CandidateSelection selection,
        IReadOnlyList<string>? availableOptions = null) =>
        new()
        {
            FieldName = fieldName,
            Status = status,
            Message = message,
            ElementDescription = candidate.Evidence.Description,
            ChosenScore = selection.Chosen?.Score,
            RunnerUpScore = selection.RunnerUp?.Score,
            ChoiceReason = selection.Chosen?.Reason,
            AvailableOptions = availableOptions ?? []
        };

    private FieldResult Complete(FieldResult result)
    {
        ConsoleReporter.PrintResult(result);
        if (result.Status is FieldStatus.Fail or FieldStatus.Ambiguous)
        {
            reportWriter.CaptureFailureScreenshot(driver, result);
        }

        return result;
    }
}
