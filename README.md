# CloudQA Dynamic Web Automation

A reusable .NET 10 console project using Selenium WebDriver 4 and Chrome. It automates only First Name, Gender, and State on the configured page. It never submits or resets the form and does not interact with other fields.

## Prerequisites and setup

- .NET 10 SDK
- Google Chrome
- Internet access on the first browser run so Selenium Manager can resolve the matching Chrome driver

Restore, build, and run the scorer tests:

```powershell
dotnet restore
dotnet build
dotnet test
```

No manually downloaded or pinned ChromeDriver is required. Selenium Manager is used by Selenium's `ChromeDriver` constructor.

## Run

Run the default live target in headed Chrome:

```powershell
dotnet run --project src/DynamicWebAutomation/DynamicWebAutomation.csproj
```

Override the URL or browser mode from the command line:

```powershell
dotnet run --project src/DynamicWebAutomation/DynamicWebAutomation.csproj -- --url "https://example.test/form"
dotnet run --project src/DynamicWebAutomation/DynamicWebAutomation.csproj -- --headless
dotnet run --project src/DynamicWebAutomation/DynamicWebAutomation.csproj -- --headed --pause 3
```

`--pause` keeps the browser open for a short number of seconds after reporting. Headed mode is the default.

## Configuration

The URL is selected in this order:

1. `--url <URL>` command-line argument
2. `CLOUDQA_TARGET_URL` environment variable
3. `TargetUrl` in `src/DynamicWebAutomation/appsettings.json`

Only absolute `http://`, `https://`, and `file://` URLs are accepted. For example, a Windows local page can be supplied as `file:///C:/path/to/page.html`.

The settings file also contains the expected First Name, Gender, and State values; page, element, and option timeouts in seconds; stale-element retry count; headless default; score and lead thresholds; and `FailWhenStateOptionMissing`. The last setting defaults to `false`: if Karnataka is absent, State is reported as skipped and the run can still pass. Set it to `true` to make that skipped condition fail the overall exit status.

## Architecture and locator approach

- `Configuration/` validates URL precedence, supported schemes, timeouts, values, and command-line options before starting Chrome.
- `Core/` creates Chrome through Selenium Manager and retries only stale-element operations.
- `Detection/` gathers visible text inputs, selects, and radio groups. The plain-data scorer ranks label evidence separately from WebDriver.
- `Automation/` runs the three requested field operations and logs candidate scores, the runner-up, and why the match was chosen.
- `Validation/` checks the value or selected state by reading it back from the page.
- `Reporting/` prints results, saves an HTML report, and captures screenshots for FAIL and AMBIGUOUS outcomes.

The framework filters by the correct visible control type. Exact semantic label evidence outranks partial label evidence; IDs, names, and placeholders are weak hints only. It accepts a match only when the configured minimum score and lead over the runner-up are both met. Competing exact visible labels or insufficient score separation produce AMBIGUOUS rather than an arbitrary choice.

Gender is detected as a radio group from shared name or group structure and the nearest preceding visible group text. The option itself is selected using its own exact text from an associated/wrapping `<label>` or adjacent text/element, not its `value` attribute. State options are waited for, searched by normalized visible text, and verified after selection. A missing option is explicitly skipped with the available options listed.

## Expected live result

The inspected CloudQA page has the First Name label association and a Gender radio group. Its State options are country names; Karnataka was not present before or after selecting India in the Country autocomplete. The current expected result is therefore two PASS results and one `SKIPPED_OPTION_NOT_AVAILABLE` result:

```text
Counts: 2 passed, 0 failed, 0 ambiguous, 1 skipped.
PASSED with 1 skipped
```

PASS is based on read-back: exact First Name value, selected Gender radio, or selected State option. A skipped field is not verified and is never counted as a pass.

## Reports and exit codes

The run writes an HTML report to `reports/`. FAIL and AMBIGUOUS results also get a screenshot there. The report records the selected/top candidate description and score, runner-up score, choice reason, result message, and available State options when relevant. Runtime reports and screenshots are excluded from git.

- `0` - all fields passed or were skipped under the configured policy
- `1` - failure or ambiguity, or a skipped State option when strict missing-option behavior is enabled
- `2` - configuration error

## Test strategy and current limitations

The xUnit project tests only plain-data scoring and selection behavior; those tests do not start a browser. Local-page compatibility cases and their results are a separate validation milestone.

This detector is designed for the inspected page and the agreed label patterns, not every website. It supports the main document and does not traverse iframes or shadow roots. It does not submit the form, test backend persistence, interact with Country, or infer dependent State data. Dynamic text, unusual control grouping, custom widgets that are not native text inputs/radios/selects, and multiple indistinguishable labels can result in a failure or AMBIGUOUS result rather than an automatic choice.
