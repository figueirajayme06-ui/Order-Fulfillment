# OF UI header actions

## Objective and decisions

Planners need trustworthy activation readiness and access to the existing order summary. Use the agreement detail header action pattern, semantic controls and localised text.

The user requested activation only after the order is fulfilled: require header raw fulfilment status 3, with a live T/A agreement, TODO/Failed activation and no requested line activation. Do not carry forward the legacy non-quote-line exception or reinterpret obsolete code 2. This changes UI eligibility only; the existing activation API and retry/cancellation contract remain unchanged.

Order summary is available for fully fulfilled A agreements, including read-only users. Construct the legacy report URL from the existing validated app configuration, an encoded agreement identifier and optional quoteId. Missing configuration produces a disabled control with a visible explanation. Open the existing report in a separate tab to preserve planning context.

## States and verification

Prevent duplicate activation while awaiting the server and refresh agreement/reservation data afterwards. Disabled activation has visible explanatory text, errors use labelled alerts, and print hides interactive controls. Test fulfilment and activation state matrices, read-only access, duplicate submission, URL encoding/base paths, missing configuration and report visibility. Run focused tests/build and combined desktop review. No dependencies, schema changes or report duplication.
