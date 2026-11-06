# Five-Minute Screen-Recording Script

## 0:00–0:35 — Introduction and problem statement

**Show:** Project title and the CloudQA form URL in the browser.

**Say:**  
“This is my CloudQA Software Testing and Automation Internship Task 1. The goal is to automate exactly three fields: enter Adhya as First Name, choose Female for Gender, and choose Karnataka for State only if it exists. A click alone does not count as a pass; the program reads the resulting value or checked state back from the page.”

## 0:35–1:15 — Project architecture

**Show:** The project folders in the editor.

**Say:**  
“The console application is under `src/DynamicWebAutomation`. Configuration handles the target URL and settings, Detection gathers visible controls and scores semantic evidence, Automation coordinates preflight and actions, Validation reads the state back, and Reporting writes console, HTML, and JSON results. xUnit tests exercise the scoring, visibility, and summary rules without starting Chrome.”

## 1:15–2:10 — Run the real CloudQA form

**Show:** A terminal at the project root. Run:

```powershell
dotnet run --project src/DynamicWebAutomation/DynamicWebAutomation.csproj -- --url "https://app.cloudqa.io/home/AutomationPracticeForm" --headed --pause 3
```

**Say while it runs:**  
“The URL can also come from the environment variable or appsettings, in that order after the command-line option. The output shows each candidate score, its runner-up, and why the detector chose it. The app preflights all three fields before it changes any value.”

## 2:10–2:50 — Demonstrate First Name and Gender verification

**Show:** The browser and console output.

**Say:**  
“First Name is cleared, filled, and read back as Adhya. Gender is located as a radio group from nearby text. The option text Female comes from its visible adjacent label text. Selenium performs a native click, then the app reports the group name, option count, selected option text, and checked state. If the click is intercepted, the app reports failure rather than JavaScript-clicking around it.”

## 2:50–3:25 — Explain why Karnataka is skipped

**Show:** The State dropdown in the browser and the console’s available-options line.

**Say:**  
“The live page’s State select currently shows country names and does not contain Karnataka. I also inspected it after selecting India in the Country autocomplete; Karnataka still did not appear. The app therefore reports `SKIPPED_OPTION_NOT_AVAILABLE`. This means State was not verified. The final message says ‘PASSED with 1 skipped’ under the default policy; Karnataka did not pass on the live site.”

## 3:25–4:20 — Show modified HTML scenarios

**Show:** `test-pages/v2-changed-identifiers.html`, `v4-similar-and-hidden-fields.html`, and `n2-ambiguous-first-name.html`.

**Say:**  
“The local pages exercise changed IDs, changed order, wrapping labels, similar-looking candidates, and hidden duplicates. The detector is not told which fixture it is viewing; only the URL changes. In the ambiguous page there are two visible exact First Name labels, so preflight reports AMBIGUOUS and does not modify any field. The hidden-duplicate case excludes the input under a hidden ancestor.”

## 4:20–4:45 — Show tests and reports

**Show:** Run:

```powershell
dotnet test
.\test-pages\run-all.ps1
```

Then show `reports/integration/` and an HTML report.

**Say:**  
“The unit tests cover scoring, visibility, and skipped-summary behavior. The PowerShell runner reads its expected outcomes from `test-pages/expected-results.md`, compares them with each process exit and JSON result, and recognizes expected negative application exits separately from unexpected execution failures.”

## 4:45–5:00 — Limitations and conclusion

**Show:** The report or README limitations section.

**Say:**  
“This is not a universal locator for every website. It currently handles the tested main-document native inputs, radios, and selects. Placeholder-only fields and custom div dropdowns are known limitations; the tests record their actual failures rather than tuning the detector to force a pass. I’ve kept the scope to the three requested fields and verified the results through browser read-back.”
