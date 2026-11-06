# Technical Approach

## Problem statement

The task is to automate exactly three form fields—First Name, Gender, and State—without depending on brittle fixed selectors. The target URL and expected values are configurable. A result is PASS only after the browser state is read back and confirmed. If Karnataka is not in the State options, the field is reported as skipped, not passed.

## Architecture

The console application separates responsibilities without introducing a large framework:

- **Configuration** resolves and validates URL precedence, field values, timeouts, retries, and thresholds.
- **Core** creates Chrome through Selenium Manager and provides stale-element retry and a reusable visibility predicate.
- **Detection** gathers candidate text evidence and uses a plain-data scorer to rank candidates independently of WebDriver.
- **Automation** preflights all three targets, then performs only the requested operations.
- **Validation** confirms typed values, checked radios, and selected option text by reading page state.
- **Reporting** produces console output, HTML, JSON, and screenshots for failures or ambiguity.

## Dynamic locator strategy

The collector filters to visible native controls of the requested type. It uses label-to-control relationships, wrapping labels, ARIA labels, nearby visible group text, and finally weaker placeholder/name/ID hints. Text is normalized by trimming, collapsing whitespace, and ignoring case. No page-specific ID, index, fixed XPath, or absolute position is used as the primary locator.

For Gender, radios are grouped by shared name or semantic/container grouping. The group is matched against nearby `Gender` text. Each option is identified from its own associated/wrapping label or adjacent element/text node; its `value` is not used as option-label evidence.

## Confidence scoring and ambiguity

Evidence is represented as plain data and ranked in `CandidateScorer`. Current exact evidence weights are 100 for label-for, 95 for wrapping label, 90 for `aria-labelledby`, 85 for `aria-label`, and 80 for nearby group text. Partial semantic evidence receives half weight; weak attribute hints score 20 exact and 10 partial.

The default minimum score is 60 and minimum lead is 15. More than one visible exact semantic label, a tie, or insufficient score separation yields AMBIGUOUS. A best score below the threshold is rejected. This design favors a transparent failure over silently acting on the wrong element.

## Candidate visibility

The collector captures each candidate’s and ancestor’s computed display, visibility, opacity, and rendered bounds. `PageVisibilityChecker` applies the rule to plain snapshots: every node must not be `display:none`, hidden/collapsed, or zero-opacity, and must have positive width and height. Hidden duplicates are excluded before the scorer sees them. The helper is independently unit-tested.

This is not complete browser hit-testing. A visible element can still be covered by an overlay; in that case, the native Selenium action failure is reported.

## Preflight and actions

The application collects and scores First Name, Gender, and State before modifying any form value. Gender option text and the State option list are also checked in preflight. If any target is ambiguous, no field is changed; ambiguous fields are reported and unaffected fields are marked NOT_RUN. Missing targets are failures but are distinct from ambiguity.

After successful preflight:

1. First Name is cleared, filled, and verified by exact value read-back.
2. The exact-text Female radio is clicked using native Selenium and verified with checked state and selected option text.
3. The State select waits for options, selects exact normalized visible text if present, and verifies the selected option. If the option is absent, the result is `SKIPPED_OPTION_NOT_AVAILABLE`.

## Error handling and reporting

Settings errors exit with code 2. Field failures, ambiguity, and strict missing-option failures exit with code 1. Passing runs and configured skips exit with code 0. Stale element references are retried by re-detecting the affected target up to the configured count. Other WebDriver errors are reported explicitly. Chrome is quit in a `finally` block.

Every execution writes structured JSON. Field results include status, expected/actual value, candidate description and score, runner-up score, choice reason, options, radio diagnostics, and screenshot information. A readable HTML report is also written; FAIL and AMBIGUOUS results trigger screenshots when available.

## Testing strategy and verified evidence

- xUnit tests exercise scoring, type filtering, ties/margins, whitespace normalization, visibility snapshots, and skipped-summary exit-code rules without a browser.
- Eleven local HTML fixtures exercise changed identifiers/order, similar and hidden fields, wrapping labels, missing/ambiguous targets, unavailable Karnataka, missing Female, and unsupported markup.
- The PowerShell runner reads a single expected-results table and compares process exit, JSON exit, field statuses, and execution errors.
- The live run verified First Name and Female, and skipped State because Karnataka was not available.

## Technical limitations

The framework supports the main document and native input/radio/select elements represented by the tested evidence patterns. It does not traverse iframes or shadow roots, support arbitrary custom div widgets, submit the form, or verify backend persistence. Placeholder-only evidence is intentionally too weak to select a field. Visibility checks do not detect every occlusion or viewport interaction issue. The solution is not claimed to work universally across websites.
