# CloudQA Dynamic Web Automation — Task 1

## Project overview

This internship assignment is a reusable C# console automation framework for checking three fields on the CloudQA Automation Practice Form:

1. First Name — enter `Adhya`.
2. Gender — select `Female`.
3. State — select `Karnataka` only when that option is available.

The program uses Selenium WebDriver 4 with Chrome. It locates fields from visible semantic evidence rather than relying on one fixed page-specific selector. It verifies results by reading values and selected states back from the page. It does not submit or reset the form and does not interact with other fields.

## Features

- Configurable URL precedence: command line, environment variable, then JSON settings.
- Accepts absolute `http://`, `https://`, and `file://` target URLs.
- ChromeDriver startup through Selenium Manager.
- Configurable values, timeouts, retries, headless mode, and scoring thresholds.
- Visible-candidate filtering and conservative ambiguity detection.
- Read-only preflight of all three targets before changing a field; ambiguity stops the run before interactions.
- Native Selenium click for the Gender radio; intercepted clicks are reported, not bypassed with a JavaScript click.
- State selection only when the configured option exists; missing option is reported as skipped, not passed.
- Console, HTML, and structured JSON results; screenshots for FAIL and AMBIGUOUS results.
- xUnit scorer, visibility, and summary tests plus local HTML integration scenarios.

## Technology stack

| Technology | Use |
|---|---|
| C# / .NET 10 | Console application and unit tests |
| Selenium WebDriver 4 | Browser automation |
| Chrome | Browser under test |
| Selenium Manager | Resolves the compatible Chrome driver |
| xUnit | Plain-data unit tests |
| PowerShell | Local fixture runner and expected/actual comparison |

## Prerequisites

- .NET 10 SDK
- Google Chrome
- PowerShell on Windows for `test-pages/run-all.ps1`
- Internet access on the first Selenium Manager run if the matching driver is not already available

No manually installed or hard-coded ChromeDriver path is required. In this environment the SDK was available at `C:\Program Files\dotnet\dotnet.exe`; if `dotnet` is not on PATH, invoke that executable directly.

## Installation and setup

From the project root:

```powershell
dotnet restore
dotnet build
dotnet test
```

The main settings file is `src/DynamicWebAutomation/appsettings.json`. Current field values are `Adhya`, `Female`, and `Karnataka`. Timeouts are configured in seconds. `FailWhenStateOptionMissing` defaults to `false`.

## Project structure

```text
CloudQaAutomation.slnx
src/
  DynamicWebAutomation/
    Automation/       Three-field preflight and automation flow
    Configuration/   URL precedence, settings and validation
    Core/             Chrome setup, stale retry and visibility helper
    Detection/        Visible evidence collection and plain-data scoring
    Models/           Candidate, field-result and summary data
    Reporting/        Console, HTML, JSON and failure screenshots
    Validation/       Read-back checks
    appsettings.json  Values, timeouts, retries and thresholds
tests/
  DynamicWebAutomation.Tests/
    CandidateScorerTests.cs
    PageVisibilityCheckerTests.cs
    RunSummaryTests.cs
test-pages/
  v1-original.html
  v2-changed-identifiers.html
  v3-changed-order.html
  v4-similar-and-hidden-fields.html
  v5-wrapping-labels.html
  n1-missing-first-name.html
  n2-ambiguous-first-name.html
  n3-no-karnataka.html
  n4-no-female-option.html
  l1-placeholder-only.html
  l2-custom-div-dropdown.html
  expected-results.md
  run-all.ps1
reports/              Generated HTML, JSON and screenshots (ignored by Git)
README.md
TECHNICAL_APPROACH.md
INTERVIEW_GUIDE.md
DEMO_SCRIPT.md
```

## Build and run

Run the configured live URL in headed Chrome:

```powershell
dotnet run --project src/DynamicWebAutomation/DynamicWebAutomation.csproj
```

Examples:

```powershell
# Override the URL for one run
dotnet run --project src/DynamicWebAutomation/DynamicWebAutomation.csproj -- --url "https://example.test/form"

# Run a local HTML file
dotnet run --project src/DynamicWebAutomation/DynamicWebAutomation.csproj -- --url "file:///C:/path/to/page.html" --headless

# Force headed mode and keep Chrome visible for three seconds after reporting
dotnet run --project src/DynamicWebAutomation/DynamicWebAutomation.csproj -- --headed --pause 3

# Write structured results to a caller-selected JSON file
dotnet run --project src/DynamicWebAutomation/DynamicWebAutomation.csproj -- --url "file:///C:/path/to/page.html" --headless --results-json "C:/temp/result.json"
```

Headed mode is the default. `--pause` without a number uses a short default pause. `--headless` and `--headed` cannot be used together.

### Configure a different URL

The URL is chosen in this order:

1. `--url <URL>` command-line argument.
2. `CLOUDQA_TARGET_URL` environment variable.
3. `TargetUrl` in `src/DynamicWebAutomation/appsettings.json`.

PowerShell environment-variable example:

```powershell
$env:CLOUDQA_TARGET_URL = "https://example.test/form"
dotnet run --project src/DynamicWebAutomation/DynamicWebAutomation.csproj
```

Only absolute HTTP, HTTPS, and file URLs are accepted. A local Windows file can be written as `file:///C:/folder/page.html`.

## Dynamic detection and scoring

The collector considers visible native text inputs, radio inputs, and `<select>` controls of the expected type. It gathers label/for relationships, wrapping labels, `aria-labelledby`, `aria-label`, nearby group text, and weak attribute hints. IDs, names, and placeholders are not primary evidence.

Text comparison trims, collapses whitespace, and ignores case. Exact semantic evidence outranks partial evidence. In the current scoring implementation, exact label-for evidence scores 100, wrapping-label text 95, `aria-labelledby` 90, `aria-label` 85, and nearby group text 80. Partial semantic evidence receives half that evidence weight. Exact and partial weak attribute hints score 20 and 10. The defaults require a score of at least 60 and a lead of at least 15 over the runner-up. Multiple visible exact labels or a tie/too-small lead produces AMBIGUOUS instead of selecting arbitrarily.

## Gender radio-group detection

The framework groups visible radios using a shared name or a semantic group/container. It scores the group against nearby visible `Gender` text, then reads each radio’s own option text from an associated/wrapping `<label>` or adjacent text/element. It chooses only an exact normalized text match for `Female`; the radio `value` is not treated as its label.

The console and JSON include group name, option count, selected option text, and checked-state read-back. The choice uses a native Selenium click. If the browser intercepts the click, the run reports failure and does not fall back to JavaScript click.

## State dropdown handling

The framework looks for a native `<select>` associated with the `State` label, waits for its options, and matches the configured value by normalized visible option text. It reads the selected option back before reporting PASS. If Karnataka is absent, it emits `SKIPPED_OPTION_NOT_AVAILABLE` with the available options. A skipped value is not verified and is not counted as a pass. With the default `FailWhenStateOptionMissing: false`, a skipped State can still yield exit code 0; set the flag to `true` to make that condition an overall failure.

## Visibility and ambiguity handling

Visibility is checked on a candidate and its ancestor chain. `display:none`, hidden/collapsed visibility, zero opacity, and zero-width or zero-height rendered bounds make a candidate ineligible. This excludes hidden duplicate fields before scoring. The detector preflights First Name, Gender, and State before modifying any value; any ambiguous target aborts interactions and marks the other fields NOT_RUN.

The visibility rule does not constitute a universal hit-testing or viewport-occlusion detector. Elements covered by unrelated overlays can still be considered visible; native click interception is surfaced explicitly.

## Tests and verified results

Run the complete solution build and unit test suite:

```powershell
dotnet build
dotnet test
```

Run the 11 local integration scenarios from the project root:

```powershell
.\test-pages\run-all.ps1
```

The runner reads expected outcomes from `test-pages/expected-results.md`. Expected application exits of 1 count as a successful negative-scenario test only if field statuses match and the JSON has no `executionError`.

Verified local results:

| Page | First Name | Gender | State | Exit | Result |
|---|---|---|---:|---:|---|
| `v1-original.html` | PASS | PASS | PASS | 0 | PASS |
| `v2-changed-identifiers.html` | PASS | PASS | PASS | 0 | PASS |
| `v3-changed-order.html` | PASS | PASS | PASS | 0 | PASS |
| `v4-similar-and-hidden-fields.html` | PASS | PASS | PASS | 0 | PASS |
| `v5-wrapping-labels.html` | PASS | PASS | PASS | 0 | PASS |
| `n1-missing-first-name.html` | FAIL | PASS | PASS | 1 | PASS — expected negative result |
| `n2-ambiguous-first-name.html` | AMBIGUOUS | NOT_RUN | NOT_RUN | 1 | PASS — preflight stops before interaction |
| `n3-no-karnataka.html` | PASS | PASS | SKIPPED_OPTION_NOT_AVAILABLE | 0 | PASS — configured skip behavior |
| `n4-no-female-option.html` | PASS | FAIL | PASS | 1 | PASS — no substitute radio selected |
| `l1-placeholder-only.html` | FAIL | PASS | PASS | 1 | Known limitation; no false PASS |
| `l2-custom-div-dropdown.html` | PASS | PASS | FAIL | 1 | Known limitation; native select required |

All 11 actual outcomes matched the expectation table in the completed local integration run. The seven original scorer tests were retained; with six visibility tests and one summary test, the current unit suite contains 14 passing tests.

### Actual live CloudQA result

The live page run verified First Name and Gender, but Karnataka was unavailable in the State select. The actual result was:

```text
[PASS] First Name: Typed value was read back exactly as 'Adhya'.
[PASS] Gender: Radio option text 'Female' is selected.
[SKIPPED_OPTION_NOT_AVAILABLE] State: Option 'Karnataka' is not available; this field was not verified.
Counts: 2 passed, 0 failed, 0 ambiguous, 1 skipped, 0 not run.
PASSED with 1 skipped
```

The process exited with code 0 under the default missing-option policy. **Karnataka did not pass on the live site.**

## Reports and screenshots

Reports are written under the project root’s `reports/` directory:

- `reports/report-<timestamp>.html` — readable field report.
- `reports/results-<timestamp>.json` — structured result for a standard run.
- `reports/integration/<scenario>-<run-id>.json` — one JSON result per local integration scenario.
- `reports/<timestamp>-<field>.png` — screenshot for FAIL or AMBIGUOUS results.

Reports and screenshots are runtime artifacts, ignored by Git. Existing reports should be preserved when rerunning tests.

## Known limitations and future improvements

- Designed and tested for the inspected CloudQA form and the documented fixtures; it is not universally compatible with arbitrary websites.
- Traverses the main document only; it does not switch into iframes or pierce shadow roots.
- Supports native text inputs, radio controls, and `<select>` elements, not arbitrary custom widgets. The custom div State fixture is therefore a known unsupported case.
- Placeholder-only labels are intentionally weak evidence and fall below the configured minimum score.
- The live site currently lists country names in the State control and does not expose Karnataka, so State is skipped rather than passed.
- Does not interact with Country, submit/reset the form, verify backend persistence, or test form submission.
- The visibility check handles hidden/collapsed and zero-sized ancestor cases but does not detect every overlay or viewport hit-testing condition.

Potential future work, subject to a separate scope decision, could add optional iframe/shadow-root traversal, a carefully designed custom-widget adapter, richer browser-state diagnostics, and dedicated reporting tests. None of those capabilities are claimed by the current implementation.
