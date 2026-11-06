# Interview Guide

## Explain the project simply

“This is a small .NET 10 and Selenium framework that automates only First Name, Gender, and State. It gathers visible controls and their semantic text, scores candidates without mixing the scoring logic with Selenium, checks for ambiguity before it changes anything, performs the requested actions, and reads the browser state back to verify them. It records console, HTML, and JSON results. On the inspected live CloudQA page, the first two fields pass, while State is skipped because Karnataka is not present.”

## Major components

| Component | 30-second explanation |
|---|---|
| `Program.cs` | Loads settings, starts Chrome, runs automation, writes reports, returns the application exit code, and closes Chrome in `finally`. |
| `AutomationSettings` | Reads appsettings, applies URL precedence, validates supported URL schemes and settings, and handles run options. |
| `BrowserFactory` | Creates Chrome through Selenium Manager; no hand-managed driver path is needed. |
| `CandidateCollector` | Collects visible text inputs, radio groups, and native selects plus label/nearby-text evidence. |
| `CandidateScorer` | Uses plain evidence data to rank candidates; it can be unit-tested without a browser. |
| `PageVisibilityChecker` | Applies the candidate-and-ancestor visibility rule to simple rendered-state snapshots. |
| `PracticeFormAutomation` | Preflights all three targets and only then performs the three allowed field operations. |
| `FieldValidator` | Checks the actual value, selected radio state, or selected option text after the action. |
| `ReportWriter` | Writes HTML and JSON and saves screenshots for failure or ambiguity. |
| xUnit tests | Check scoring, visibility, and summary rules with no live site dependency. |
| `run-all.ps1` | Runs local pages, reads expected outcomes from one table, and distinguishes expected application failures from unexpected execution problems. |

## Common technical questions

### Why are fixed XPath and CSS selectors fragile?

They often depend on IDs, classes, nesting, or element positions that can change during a redesign. This project prefers semantic relationships and visible text, while treating IDs, names, and placeholders as weak hints.

### How does dynamic detection work?

The collector filters visible controls to the expected type, gathers label/ARIA/nearby text evidence, and passes plain data to the scorer. The scorer normalizes whitespace and case, ranks candidates, and applies a minimum score plus a runner-up margin.

### How are changed IDs and field order handled?

IDs and positions are not primary evidence. Associated label text, wrapping labels, ARIA text, and nearby group text carry the stronger scores. The local changed-ID and changed-order variants pass.

### How does it distinguish similar-looking fields?

It compares evidence strength. Exact semantic labels outrank partial label text; weak placeholder/name/ID evidence alone scores below the current minimum. If multiple visible candidates have the same exact label or close scores, it reports AMBIGUOUS instead of choosing one.

### How are hidden fields excluded?

The candidate and every ancestor are checked for `display:none`, hidden/collapsed visibility, zero opacity, and zero-sized rendered bounds. A visible-looking input under a hidden ancestor is therefore excluded. The hidden-duplicate fixture demonstrates this behavior.

### What happens when a field is ambiguous or missing?

Ambiguity is found during preflight, so no target field is changed; other fields are marked NOT_RUN. A missing candidate becomes a field failure, but the other non-ambiguous fields can still run. Both conditions appear in JSON.

### Why use Selenium native clicks for radios?

A native click exercises the browser’s real interaction behavior and exposes overlays or interception. A JavaScript click could bypass those conditions and make the result less representative. This implementation reports interception and does not silently fall back.

### How does State selection work?

The framework waits for a native select’s options, searches normalized visible option text, selects only the configured exact match, and reads the selected text back. If the option is absent, it reports `SKIPPED_OPTION_NOT_AVAILABLE`, not PASS.

### Why is Karnataka skipped on the real CloudQA page?

During inspection and the live rerun, the State select contained country names, including India, but not Karnataka. Selecting India in the Country autocomplete did not add Karnataka. Therefore the live result is two PASS fields and one skipped/unverified State field. Karnataka did not pass on the live website.

## Exactly 15 likely interview questions and short answers

1. **What is the goal of the project?**  
   Automate only First Name, Gender, and State with configurable values and verify the resulting browser state.

2. **Why use .NET 10 and Selenium WebDriver?**  
   C#/.NET provides a clear typed console application, and Selenium drives the real Chrome browser through standard WebDriver behavior.

3. **How is the URL configured?**  
   Command-line `--url` takes precedence, then `CLOUDQA_TARGET_URL`, then `TargetUrl` in appsettings.

4. **Which URL schemes are allowed?**  
   Absolute HTTP, HTTPS, and file URLs, so the app can run both websites and local HTML fixtures.

5. **How are candidates filtered?**  
   The collector chooses visible native controls of the expected type before scoring their text evidence.

6. **What evidence receives the strongest score?**  
   An exact `<label for>` relationship scores 100 in the current scorer, ahead of wrapping, ARIA, nearby, and weak attribute evidence.

7. **How are labels compared?**  
   Whitespace is trimmed and collapsed and comparison is case-insensitive; exact matches are stronger than partial matches.

8. **What do the score threshold and margin protect against?**  
   They prevent weak or near-tied candidates from being selected automatically; those cases are rejected or marked AMBIGUOUS.

9. **How does the Gender lookup work when the heading has no `for`?**  
   It scores the radio group from nearby visible Gender text, then matches the option’s own adjacent or associated-label text exactly.

10. **Why not select a radio from its `value`?**  
    The value is not necessarily user-facing text. The implementation uses the visible option label and verifies `Selected`.

11. **How does the framework handle changing IDs or positions?**  
    IDs and order are not primary evidence; semantic label and group text are used. The changed-ID and changed-order fixture runs passed.

12. **What does preflight do?**  
    It detects all three targets before modifying values; an ambiguity aborts interactions so the app cannot partially fill the form before discovering a conflict.

13. **How do you know a field passed?**  
    First Name is read back exactly, the Female radio is checked and its selected text read, and a State option is read back after selection.

14. **Why is the live State test skipped?**  
    Karnataka was absent from the inspected live State options, so the app reports it as unavailable and unverified rather than claiming success.

15. **Does this framework work on every website?**  
    No. It supports the tested main-document native-control patterns; iframe/shadow traversal, arbitrary custom widgets, and complete overlay hit-testing are outside its current scope.
