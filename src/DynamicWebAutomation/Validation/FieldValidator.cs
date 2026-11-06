using DynamicWebAutomation.Detection;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace DynamicWebAutomation.Validation;

/// <summary>
/// Verifies browser state after an action so a successful click alone never
/// counts as a passing field.
/// </summary>
public static class FieldValidator
{
    public static bool HasExactTextValue(IWebElement element, string expected) =>
        string.Equals(element.GetAttribute("value"), expected, StringComparison.Ordinal);

    public static bool IsSelected(IWebElement element) => element.Selected;

    public static bool HasSelectedOptionText(SelectElement select, string expected) =>
        string.Equals(CandidateScorer.Normalize(select.SelectedOption.Text),
            CandidateScorer.Normalize(expected), StringComparison.Ordinal);
}
