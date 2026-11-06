using System.Text.Json;
using DynamicWebAutomation.Models;
using OpenQA.Selenium;

namespace DynamicWebAutomation.Detection;

/// <summary>
/// Collects visible controls and their nearby text without choosing a winner;
/// keeping collection separate lets the scorer work with plain data.
/// </summary>
public sealed class CandidateCollector(IWebDriver driver)
{
    private const string EvidenceScript = """
        const el = arguments[0];
        const visible = node => {
          const style = getComputedStyle(node);
          const rect = node.getBoundingClientRect();
          return rect.width > 0 && rect.height > 0 &&
            style.display !== "none" && style.visibility !== "hidden";
        };
        const texts = nodes => nodes.filter(visible)
          .map(node => (node.innerText || node.textContent || "").trim())
          .filter(text => text.length > 0);
        const forLabels = [...document.querySelectorAll("label[for]")].filter(label => label.control === el);
        const wrappingLabels = [];
        for (let parent = el.parentElement; parent; parent = parent.parentElement) {
          if (parent.tagName === "LABEL") { wrappingLabels.push(parent); break; }
        }
        const nearby = [];
        let current = el;
        for (let depth = 0; current && current.parentElement && depth < 5; depth++) {
          let sibling = current.previousSibling;
          while (sibling) {
            let text = "";
            if (sibling.nodeType === Node.ELEMENT_NODE && visible(sibling)) {
              text = (sibling.innerText || sibling.textContent || "").trim();
            } else if (sibling.nodeType === Node.TEXT_NODE) {
              text = sibling.nodeValue.trim();
            }
            if (text) {
              nearby.push(text);
              break;
            }
            sibling = sibling.previousSibling;
          }
          if (nearby.length) break;
          current = current.parentElement;
          if (current.tagName === "FORM") break;
        }
        const labelledBy = (el.getAttribute("aria-labelledby") || "")
          .split(/\s+/).filter(Boolean).map(id => document.getElementById(id))
          .filter(Boolean).map(node => node.innerText || node.textContent || "");
        const weakHints = [el.getAttribute("placeholder"), el.getAttribute("name"), el.id]
          .filter(value => value && value.trim());
        return JSON.stringify({
          tag: el.tagName,
          type: el.type || "",
          id: el.id || "",
          name: el.getAttribute("name") || "",
          placeholder: el.getAttribute("placeholder") || "",
          labelsFor: texts(forLabels),
          wrappingLabels: texts(wrappingLabels),
          ariaLabelledBy: labelledBy,
          ariaLabels: [el.getAttribute("aria-label") || ""].filter(Boolean),
          nearby,
          weakHints
        });
        """;

    private const string RadioGroupKeyScript = """
        const el = arguments[0];
        const semanticGroup = el.closest('fieldset, [role="radiogroup"]');
        let scope = semanticGroup || el.form || document;
        if (!semanticGroup && !el.name) {
          for (let parent = el.parentElement; parent && parent !== el.form; parent = parent.parentElement) {
            if ([...parent.querySelectorAll('input[type="radio"]')].filter(radio => {
              const rect = radio.getBoundingClientRect();
              return rect.width > 0 && rect.height > 0 && getComputedStyle(radio).visibility !== "hidden";
            }).length > 1) { scope = parent; break; }
          }
        }
        const source = `${el.name || "unnamed"}|${scope.outerHTML || "document"}`;
        let hash = 0;
        for (let index = 0; index < source.length; index++) {
          hash = ((hash << 5) - hash + source.charCodeAt(index)) | 0;
        }
        return `${el.name ? "name" : "container"}:${hash}`;
        """;

    private const string OptionTextScript = """
        const el = arguments[0];
        const visible = node => {
          const style = getComputedStyle(node);
          const rect = node.getBoundingClientRect();
          return rect.width > 0 && rect.height > 0 &&
            style.display !== "none" && style.visibility !== "hidden";
        };
        const result = [];
        const add = text => {
          const value = (text || "").replace(/\s+/g, " ").trim();
          if (value && !result.includes(value)) result.push(value);
        };
        for (const label of (el.labels || [])) if (visible(label)) add(label.innerText || label.textContent);
        for (const label of (el.labels || [])) if (visible(label)) add(label.innerText || label.textContent);
        for (const direction of [1, -1]) {
          let sibling = direction > 0 ? el.nextSibling : el.previousSibling;
          while (sibling) {
            if (sibling.nodeType === Node.TEXT_NODE) {
              if (sibling.nodeValue.trim()) { add(sibling.nodeValue); break; }
            } else if (sibling.nodeType === Node.ELEMENT_NODE && visible(sibling)) {
              const text = sibling.innerText || sibling.textContent || "";
              if (text.trim()) { add(text); break; }
            }
            sibling = direction > 0 ? sibling.nextSibling : sibling.previousSibling;
          }
          if (result.length) break;
        }
        return JSON.stringify(result);
        """;

    /// <summary>
    /// Finds visible text inputs or selects by browser-reported type, never by
    /// a page-specific id, class, position, or label XPath.
    /// </summary>
    public IReadOnlyList<PageCandidate> CollectFields(string controlType)
    {
        IEnumerable<IWebElement> elements = controlType switch
        {
            "text" => driver.FindElements(By.TagName("input"))
                .Where(element => string.Equals(element.GetAttribute("type") ?? "text", "text", StringComparison.OrdinalIgnoreCase)),
            "select" => driver.FindElements(By.TagName("select")),
            _ => throw new ArgumentOutOfRangeException(nameof(controlType), controlType, "Only text and select controls are supported here.")
        };

        return elements
            .Where(IsVisible)
            .Select(element => new PageCandidate(ReadEvidence(element, controlType), element, []))
            .ToArray();
    }

    /// <summary>
    /// Groups visible radio inputs by their shared name or semantic container;
    /// the group's nearby heading is scored before option text is considered.
    /// </summary>
    public IReadOnlyList<PageCandidate> CollectRadioGroups()
    {
        IWebElement[] radios = driver.FindElements(By.TagName("input"))
            .Where(element => string.Equals(element.GetAttribute("type"), "radio", StringComparison.OrdinalIgnoreCase))
            .Where(IsVisible)
            .ToArray();

        return radios
            .GroupBy(element => Convert.ToString(((IJavaScriptExecutor)driver).ExecuteScript(RadioGroupKeyScript, element)) ?? "")
            .Select(group =>
            {
                IWebElement[] members = group.ToArray();
                return new PageCandidate(ReadEvidence(members[0], "radio-group"), members[0], members);
            })
            .ToArray();
    }

    /// <summary>
    /// Reads an option's own label from an associated/wrapping label or adjacent
    /// element/text node; a radio's value is intentionally not treated as text.
    /// </summary>
    public IReadOnlyList<string> GetRadioOptionTexts(IWebElement radio)
    {
        string json = Convert.ToString(((IJavaScriptExecutor)driver).ExecuteScript(OptionTextScript, radio)) ?? "[]";
        return JsonSerializer.Deserialize<string[]>(json) ?? [];
    }

    private CandidateEvidence ReadEvidence(IWebElement element, string controlType)
    {
        string json = Convert.ToString(((IJavaScriptExecutor)driver).ExecuteScript(EvidenceScript, element))
            ?? throw new InvalidOperationException("The browser did not return candidate evidence.");

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string tag = ReadString(root, "tag");
        string type = ReadString(root, "type");
        string id = ReadString(root, "id");
        string name = ReadString(root, "name");
        string placeholder = ReadString(root, "placeholder");
        string description = $"<{tag.ToLowerInvariant()} type=\"{type}\" name=\"{name}\" id=\"{id}\">";

        return new CandidateEvidence(
            description,
            controlType,
            true,
            ReadStrings(root, "labelsFor"),
            ReadStrings(root, "wrappingLabels"),
            ReadStrings(root, "ariaLabelledBy"),
            ReadStrings(root, "ariaLabels"),
            ReadStrings(root, "nearby"),
            ReadStrings(root, "weakHints").Concat([placeholder]).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray());
    }

    private static bool IsVisible(IWebElement element) => element.Displayed;

    private static string ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) ? value.GetString() ?? "" : "";

    private static IReadOnlyList<string> ReadStrings(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value)
            ? value.EnumerateArray().Select(item => item.GetString() ?? "").Where(text => !string.IsNullOrWhiteSpace(text)).ToArray()
            : [];
}

/// <summary>
/// Keeps the selected DOM element alongside the plain data sent to the scorer.
/// </summary>
public sealed record PageCandidate(
    CandidateEvidence Evidence,
    IWebElement Element,
    IReadOnlyList<IWebElement> RadioMembers);
