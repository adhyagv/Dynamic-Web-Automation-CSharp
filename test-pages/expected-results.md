# Local integration expectations

The PowerShell runner reads this table as the single source of fixture outcomes.
`NOT_RUN` means preflight detected ambiguity and correctly stopped before changes.
An expected application exit code of 1 is a successful negative-scenario test when
the field statuses match and `executionError` is empty.

| Page | First Name | Gender | State | Exit | Reason |
|---|---|---|---|---:|---|
| v1-original.html | PASS | PASS | PASS | 0 | Baseline semantic labels and adjacent radio text. |
| v2-changed-identifiers.html | PASS | PASS | PASS | 0 | Changed IDs and names must not affect semantic detection. |
| v3-changed-order.html | PASS | PASS | PASS | 0 | Field order changes without changing their meaning. |
| v4-similar-and-hidden-fields.html | PASS | PASS | PASS | 0 | Exact visible field wins; hidden duplicate must be excluded. |
| v5-wrapping-labels.html | PASS | PASS | PASS | 0 | Wrapping labels and associated radio labels are supported. |
| n1-missing-first-name.html | FAIL | PASS | PASS | 1 | Required First Name label/control is missing. |
| n2-ambiguous-first-name.html | AMBIGUOUS | NOT_RUN | NOT_RUN | 1 | Two visible exact First Name labels must stop the run in preflight. |
| n3-no-karnataka.html | PASS | PASS | SKIPPED_OPTION_NOT_AVAILABLE | 0 | State options are present but Karnataka is absent; skip is not a pass. |
| n4-no-female-option.html | PASS | FAIL | PASS | 1 | Female is absent; no other radio option may be selected as a substitute. |
| l1-placeholder-only.html | FAIL | PASS | PASS | 1 | Placeholder alone is weak evidence below the configured score threshold. |
| l2-custom-div-dropdown.html | PASS | PASS | FAIL | 1 | State is a custom div list, not a native select supported by this detector. |
