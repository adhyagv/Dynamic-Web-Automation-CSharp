using DynamicWebAutomation.Configuration;
using DynamicWebAutomation.Detection;
using DynamicWebAutomation.Models;
using DynamicWebAutomation.Reporting;
using DynamicWebAutomation.Validation;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace DynamicWebAutomation.Automation;

/// <summary>
/// Detects all three targets before changing a field, then verifies each action
/// by reading its resulting value or checked state back from Chrome.
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
    private readonly WebDriverWait optionsWait;

    public PracticeFormAutomation(IWebDriver driver, AutomationSettings settings, ReportWriter reportWriter)
    {
        this.driver = driver;
        this.settings = settings;
        this.reportWriter = reportWriter;
        collector = new CandidateCollector(driver);
        optionsWait = new WebDriverWait(driver, TimeSpan.FromSeconds(settings.Timeouts.OptionsSeconds));
    }

    /// <summary>
    /// Performs a read-only preflight for every target. Any ambiguous target
    /// stops the run before the first field is modified.
    /// </summary>
    public IReadOnlyList<FieldResult> Run()
    {
        Console.WriteLine("Preflight: locating all three requested fields before changing any value...");
        FieldPlan firstName = DetectField(FirstNameLabel, settings.Fields.FirstName, "text");
        FieldPlan gender = DetectGender();
        FieldPlan state = DetectState();

        FieldPlan[] plans = [firstName, gender, state];
        FieldPlan[] ambiguousPlans = plans.Where(plan => plan.IsAmbiguous).ToArray();
        if (ambiguousPlans.Length > 0)
        {
            Console.WriteLine("Preflight found ambiguity; no form field will be changed.");
            return plans.Select(plan =>
            {
                FieldResult result = plan.IsAmbiguous
                    ? CreatePreflightAmbiguity(plan)
                    : new FieldResult
                    {
                        FieldName = plan.FieldName,
                        Status = FieldStatus.NotRun,
                        ExpectedValue = plan.ExpectedValue,
                        Message = $"Not run because preflight found ambiguity in {string.Join(", ", ambiguousPlans.Select(item => item.FieldName))}."
                    };
                return Complete(result);
            }).ToArray();
        }

        return
        [
            RunWithStaleRetry(
                firstName,
                () => DetectField(FirstNameLabel, settings.Fields.FirstName, "text"),
                RunFirstName),
            RunWithStaleRetry(gender, DetectGender, RunGender),
            RunWithStaleRetry(state, DetectState, RunState)
        ];
    }

    private FieldPlan DetectField(string fieldName, string target, string controlType)
    {
        Console.WriteLine($"Looking for {fieldName} {controlType} control...");
        IReadOnlyList<PageCandidate> candidates = collector.CollectFields(controlType);
        CandidateSelection selection = Select(candidates, fieldName, controlType);
        LogSelection(fieldName, selection);
        PageCandidate? candidate = FindChosen(candidates, selection);
        return new FieldPlan(fieldName, target, candidates, selection, candidate, [], [], null);
    }

    private FieldPlan DetectGender()
    {
        Console.WriteLine("Looking for Gender radio group...");
        IReadOnlyList<PageCandidate> groups = collector.CollectRadioGroups();
        CandidateSelection selection = Select(groups, GenderLabel, "radio-group");
        LogSelection(GenderLabel, selection);
        PageCandidate? group = FindChosen(groups, selection);
        if (group is null)
        {
            return new FieldPlan(GenderLabel, settings.Fields.Gender, groups, selection, null, [], [], null);
        }

        string groupName = group.RadioMembers.FirstOrDefault()?.GetAttribute("name") ?? "(unnamed)";
        Console.WriteLine($"[Gender group] name='{groupName}', options={group.RadioMembers.Count}.");

        string expected = CandidateScorer.Normalize(settings.Fields.Gender);
        IReadOnlyList<IWebElement> matchingOptions = group.RadioMembers
            .Where(radio => collector.GetRadioOptionTexts(radio)
                .Any(text => CandidateScorer.Normalize(text) == expected))
            .ToArray();

        Console.WriteLine(
            $"[Gender option preflight] exact text '{settings.Fields.Gender}' matches {matchingOptions.Count} radio option(s).");
        return new FieldPlan(
            GenderLabel,
            settings.Fields.Gender,
            groups,
            selection,
            group,
            matchingOptions,
            [],
            null,
            matchingOptions.Count > 1);
    }

    private FieldPlan DetectState()
    {
        Console.WriteLine($"Looking for {StateLabel} select and waiting for its options...");
        IReadOnlyList<PageCandidate> candidates = collector.CollectFields("select");
        CandidateSelection selection = Select(candidates, StateLabel, "select");
        LogSelection(StateLabel, selection);
        PageCandidate? candidate = FindChosen(candidates, selection);
        if (candidate is null)
        {
            return new FieldPlan(StateLabel, settings.Fields.State, candidates, selection, null, [], [], null);
        }

        SelectElement select = new(candidate.Element);
        string expected = CandidateScorer.Normalize(settings.Fields.State);
        optionsWait.Until(_ => candidate.Element.Displayed &&
            (select.Options.Count > 1 ||
             select.Options.Any(option => CandidateScorer.Normalize(option.Text) == expected)));

        string[] options = select.Options
            .Select(option => (option.Text ?? "").Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
        string[] matches = options
            .Where(text => CandidateScorer.Normalize(text) == expected)
            .ToArray();

        Console.WriteLine($"[State preflight] available options: {string.Join(", ", options)}");
        return new FieldPlan(
            StateLabel,
            settings.Fields.State,
            candidates,
            selection,
            candidate,
            [],
            options,
            matches.Length > 1 ? $"Option text '{settings.Fields.State}' appears more than once." : null);
    }

    private FieldResult RunFirstName(FieldPlan plan)
    {
        if (plan.Candidate is null || plan.Selection.Status != SelectionStatus.Selected)
        {
            return Complete(SelectionFailure(plan));
        }

        try
        {
            plan.Candidate.Element.Clear();
            plan.Candidate.Element.SendKeys(settings.Fields.FirstName);
            string actual = plan.Candidate.Element.GetAttribute("value") ?? "";
            bool passed = FieldValidator.HasExactTextValue(plan.Candidate.Element, settings.Fields.FirstName);
            return Complete(CreateResult(
                plan,
                passed ? FieldStatus.Pass : FieldStatus.Fail,
                passed
                    ? $"Typed value was read back exactly as '{actual}'."
                    : $"Expected '{settings.Fields.FirstName}' but read back '{actual}'.",
                actual));
        }
        catch (StaleElementReferenceException)
        {
            throw;
        }
        catch (WebDriverException exception)
        {
            return Complete(ActionFailure(plan, $"Could not set First Name: {exception.GetType().Name}: {exception.Message}"));
        }
    }

    private FieldResult RunGender(FieldPlan plan)
    {
        if (plan.Candidate is null || plan.Selection.Status != SelectionStatus.Selected)
        {
            return Complete(SelectionFailure(plan));
        }

        if (plan.OptionMatches.Count == 0)
        {
            return Complete(CreateResult(
                plan,
                FieldStatus.Fail,
                $"Radio group was detected but option text '{settings.Fields.Gender}' was not found.",
                null));
        }

        IWebElement option = plan.OptionMatches[0];
        string optionText = collector.GetRadioOptionTexts(option)
            .First(text => CandidateScorer.Normalize(text) == CandidateScorer.Normalize(settings.Fields.Gender));
        string groupName = plan.Candidate.RadioMembers.FirstOrDefault()?.GetAttribute("name") ?? "(unnamed)";
        try
        {
            ((IJavaScriptExecutor)driver).ExecuteScript(
                "arguments[0].scrollIntoView({ block: 'center', inline: 'nearest' });",
                option);
            option.Click();
            bool checkedAfterClick = FieldValidator.IsSelected(option);
            IWebElement? selectedRadio = plan.Candidate.RadioMembers.FirstOrDefault(FieldValidator.IsSelected);
            string? checkedText = selectedRadio is null
                ? null
                : collector.GetRadioOptionTexts(selectedRadio).FirstOrDefault();
            bool passed = checkedAfterClick &&
                          ReferenceEquals(selectedRadio, option) &&
                          CandidateScorer.Normalize(checkedText) == CandidateScorer.Normalize(settings.Fields.Gender);

            Console.WriteLine(
                $"[Gender read-back] group='{groupName}', options={plan.Candidate.RadioMembers.Count}, " +
                $"selected text='{checkedText ?? "(none)"}', checked={checkedAfterClick}.");
            return Complete(CreateResult(
                plan,
                passed ? FieldStatus.Pass : FieldStatus.Fail,
                passed
                    ? $"Radio option text '{checkedText}' is selected."
                    : $"Expected '{settings.Fields.Gender}'; read-back selected text '{checkedText ?? "(none)"}', checked={checkedAfterClick}.",
                checkedText,
                groupName,
                plan.Candidate.RadioMembers.Count,
                checkedText,
                checkedAfterClick,
                optionText));
        }
        catch (ElementClickInterceptedException exception)
        {
            return Complete(CreateResult(
                plan,
                FieldStatus.Fail,
                $"Native radio click was intercepted; no JavaScript click fallback was used: {exception.Message}",
                null,
                groupName,
                plan.Candidate.RadioMembers.Count,
                optionText,
                false,
                optionText));
        }
        catch (StaleElementReferenceException)
        {
            throw;
        }
        catch (WebDriverException exception)
        {
            return Complete(CreateResult(
                plan,
                FieldStatus.Fail,
                $"Could not select Gender with a native WebDriver click: {exception.GetType().Name}: {exception.Message}",
                null,
                groupName,
                plan.Candidate.RadioMembers.Count,
                optionText,
                false,
                optionText));
        }
    }

    private FieldResult RunState(FieldPlan plan)
    {
        if (plan.Candidate is null || plan.Selection.Status != SelectionStatus.Selected)
        {
            return Complete(SelectionFailure(plan));
        }

        if (plan.OptionError is not null)
        {
            return Complete(CreateResult(plan, FieldStatus.Ambiguous, plan.OptionError, null, availableOptions: plan.AvailableOptions));
        }

        string[] matchingOptions = plan.AvailableOptions
            .Where(text => CandidateScorer.Normalize(text) == CandidateScorer.Normalize(settings.Fields.State))
            .ToArray();
        if (matchingOptions.Length == 0)
        {
            return Complete(CreateResult(
                plan,
                FieldStatus.SkippedOptionNotAvailable,
                $"Option '{settings.Fields.State}' is not available; this field was not verified.",
                null,
                availableOptions: plan.AvailableOptions));
        }

        try
        {
            SelectElement select = new(plan.Candidate.Element);
            select.SelectByText(matchingOptions[0]);
            bool passed = FieldValidator.HasSelectedOptionText(select, settings.Fields.State);
            string selectedText = select.SelectedOption.Text.Trim();
            return Complete(CreateResult(
                plan,
                passed ? FieldStatus.Pass : FieldStatus.Fail,
                passed
                    ? $"Selected option was read back as '{selectedText}'."
                    : $"Expected selected option '{settings.Fields.State}' but read back '{selectedText}'.",
                selectedText,
                availableOptions: plan.AvailableOptions));
        }
        catch (StaleElementReferenceException)
        {
            throw;
        }
        catch (WebDriverException exception)
        {
            return Complete(ActionFailure(plan, $"Could not select State: {exception.GetType().Name}: {exception.Message}"));
        }
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

    private FieldResult RunWithStaleRetry(
        FieldPlan initialPlan,
        Func<FieldPlan> detectAgain,
        Func<FieldPlan, FieldResult> action)
    {
        FieldPlan currentPlan = initialPlan;
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return action(currentPlan);
            }
            catch (StaleElementReferenceException exception) when (attempt < settings.RetryCount)
            {
                Console.WriteLine(
                    $"[{currentPlan.FieldName}] stale element; re-detecting before retry {attempt + 1}/{settings.RetryCount}: {exception.Message}");
                currentPlan = detectAgain();
            }
            catch (StaleElementReferenceException exception)
            {
                return Complete(ActionFailure(
                    currentPlan,
                    $"Element remained stale after {settings.RetryCount} retry attempt(s): {exception.Message}"));
            }
        }
    }

    private static PageCandidate? FindChosen(IReadOnlyList<PageCandidate> candidates, CandidateSelection selection) =>
        selection.Chosen is null
            ? null
            : candidates.FirstOrDefault(candidate => ReferenceEquals(candidate.Evidence, selection.Chosen.Candidate));

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

    private static FieldResult SelectionFailure(FieldPlan plan)
    {
        ScoredCandidate? top = plan.Selection.Chosen;
        return new FieldResult
        {
            FieldName = plan.FieldName,
            Status = plan.Selection.Status == SelectionStatus.Ambiguous ? FieldStatus.Ambiguous : FieldStatus.Fail,
            ExpectedValue = plan.ExpectedValue,
            Message = plan.Selection.Message,
            ElementDescription = top?.Candidate.Description,
            ChosenScore = top?.Score,
            RunnerUpScore = plan.Selection.RunnerUp?.Score,
            ChoiceReason = top?.Reason
        };
    }

    private static FieldResult CreatePreflightAmbiguity(FieldPlan plan)
    {
        FieldResult result = SelectionFailure(plan);
        if (plan.OptionMatches.Count > 1)
        {
            result = new FieldResult
            {
                FieldName = plan.FieldName,
                Status = FieldStatus.Ambiguous,
                ExpectedValue = plan.ExpectedValue,
                Message = $"More than one radio option has the exact text '{plan.ExpectedValue}'.",
                ElementDescription = plan.Candidate?.Evidence.Description,
                ChosenScore = plan.Selection.Chosen?.Score,
                RunnerUpScore = plan.Selection.RunnerUp?.Score,
                ChoiceReason = plan.Selection.Chosen?.Reason,
                GroupName = plan.Candidate?.RadioMembers.FirstOrDefault()?.GetAttribute("name") ?? "(unnamed)",
                RadioOptionCount = plan.Candidate?.RadioMembers.Count
            };
        }
        else if (plan.OptionError is not null)
        {
            result = new FieldResult
            {
                FieldName = plan.FieldName,
                Status = FieldStatus.Ambiguous,
                ExpectedValue = plan.ExpectedValue,
                Message = plan.OptionError,
                ElementDescription = plan.Candidate?.Evidence.Description,
                ChosenScore = plan.Selection.Chosen?.Score,
                RunnerUpScore = plan.Selection.RunnerUp?.Score,
                ChoiceReason = plan.Selection.Chosen?.Reason,
                AvailableOptions = plan.AvailableOptions
            };
        }

        return result;
    }

    private static FieldResult ActionFailure(FieldPlan plan, string message) =>
        new()
        {
            FieldName = plan.FieldName,
            Status = FieldStatus.Fail,
            ExpectedValue = plan.ExpectedValue,
            Message = message,
            ElementDescription = plan.Selection.Chosen?.Candidate.Description,
            ChosenScore = plan.Selection.Chosen?.Score,
            RunnerUpScore = plan.Selection.RunnerUp?.Score,
            ChoiceReason = plan.Selection.Chosen?.Reason
        };

    private static FieldResult CreateResult(
        FieldPlan plan,
        FieldStatus status,
        string message,
        string? actualValue,
        string? groupName = null,
        int? radioOptionCount = null,
        string? selectedOptionText = null,
        bool? readBackChecked = null,
        string? elementDescriptionSuffix = null,
        IReadOnlyList<string>? availableOptions = null)
    {
        ScoredCandidate? top = plan.Selection.Chosen;
        return new FieldResult
        {
            FieldName = plan.FieldName,
            Status = status,
            ExpectedValue = plan.ExpectedValue,
            ActualValue = actualValue,
            Message = message,
            ElementDescription = top is null
                ? null
                : elementDescriptionSuffix is null
                    ? top.Candidate.Description
                    : $"{top.Candidate.Description}; option text '{elementDescriptionSuffix}'",
            ChosenScore = top?.Score,
            RunnerUpScore = plan.Selection.RunnerUp?.Score,
            ChoiceReason = top?.Reason ?? plan.Selection.Message,
            AvailableOptions = availableOptions ?? [],
            GroupName = groupName,
            RadioOptionCount = radioOptionCount,
            SelectedOptionText = selectedOptionText,
            ReadBackChecked = readBackChecked
        };
    }

    private FieldResult Complete(FieldResult result)
    {
        ConsoleReporter.PrintResult(result);
        if (result.Status is FieldStatus.Fail or FieldStatus.Ambiguous)
        {
            reportWriter.CaptureFailureScreenshot(driver, result);
        }

        return result;
    }

    private sealed record FieldPlan(
        string FieldName,
        string ExpectedValue,
        IReadOnlyList<PageCandidate> Candidates,
        CandidateSelection Selection,
        PageCandidate? Candidate,
        IReadOnlyList<IWebElement> OptionMatches,
        IReadOnlyList<string> AvailableOptions,
        string? OptionError,
        bool OptionAmbiguous = false)
    {
        public bool IsAmbiguous =>
            Selection.Status == SelectionStatus.Ambiguous ||
            OptionAmbiguous ||
            OptionError is not null;
    }
}
