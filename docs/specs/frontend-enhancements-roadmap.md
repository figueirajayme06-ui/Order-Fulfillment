# Frontend enhancement roadmap: September 2026

> **Status:** Complete — implemented, merged, and manually UAT-validated on 2 September 2026
> **Source:** Product notes and reference screenshots supplied on 2 September 2026

## Purpose

This roadmap turns the September frontend notes into bounded feature requests that a coding agent can inspect, plan,
implement, and verify independently. Each linked specification is the source of truth for that feature; this document
only defines priority, dependencies, and delivery order.

## Proposed backlog

| ID    | Feature request                                                                                      | Priority | Size / risk     | Main reason for the rating                                                             |
| ----- | ---------------------------------------------------------------------------------------------------- | -------- | --------------- | -------------------------------------------------------------------------------------- |
| FE-01 | [Read-only user role](read-only-user-role.md)                                                        | P0       | Large / high    | Security-sensitive and cross-cuts every operational mutation.                          |
| FE-02 | [Availability stock focus and asset schedule context](availability-stock-focus-and-asset-context.md) | P1       | Medium / medium | High-value fulfilment workflow; existing NOF and CPQ patterns can be reused.           |
| FE-03 | [Rich calendar-column filters](calendar-column-filters.md)                                           | P1       | Medium / low    | Existing shared date controls make the interaction practical without a new dependency. |
| FE-04 | [Ringfence warehouse and target-picker usability](ringfence-selector-usability.md)                   | P1       | Small / low     | Mostly bounded client-side changes plus a server-side creation rule.                   |
| FE-05 | [Last NOF access audit timestamp](user-last-login-audit.md)                                          | P2       | Small / medium  | Requires a database change, but the write and display paths are narrow.                |
| FE-06 | [Configurable Agreement and Asset table columns](configurable-table-columns.md)                      | P2       | Large / medium  | Feasible, but the split table header/body geometry needs an explicit spike.            |
| FE-07 | [Share saved views with named users](saved-view-user-sharing.md)                                     | P2       | Medium / medium | Requires persisted recipients and changes to the saved-view API contract.              |

Priority is a recommendation, not a release commitment. FE-01 should be treated as a security feature and reviewed
accordingly.

## Delivery order and agent boundaries

The following lanes may be worked independently. Items within a lane should be completed in order because they touch
the same contracts and files.

| Lane                  | Recommended order             | Shared change surface                                                          |
| --------------------- | ----------------------------- | ------------------------------------------------------------------------------ |
| Access and audit      | FE-01, then FE-05             | Users schema/model, Admin API/page, authentication identity.                   |
| Saved-grid experience | FE-03, then FE-06, then FE-07 | Saved-view state, Agreement/Asset tables, view controls and tests.             |
| Availability          | FE-02                         | `AvailabilityPanel`, Asset event lookup, availability tests and specification. |
| Ringfence             | FE-04                         | Ringfence editor and Asset-to-Ringfence picker.                                |

An agent should receive one linked feature specification, the repository `AGENTS.md`,
`src/OF.Frontend/AGENTS.md` for user-facing work, and only the directly relevant API/UX documentation. Do not ask two
agents to edit items in the same lane concurrently unless one is doing read-only investigation.

## Cross-feature decisions

- **Current application only:** user-facing work belongs in `src/OF.Frontend`; `src/OF.UI` is reference material.
- **No grid replacement:** the configurable-column request must not introduce a commercial or heavyweight grid without
  a separate product and dependency decision.
- **Saved-view compatibility:** FE-03 and FE-06 must preserve existing version-1 Agreement and Asset saved states. FE-07
  changes sharing metadata, not the meaning of a saved state.
- **Read-only precedence:** FE-01 denies operational writes even if a malformed account also carries another role.
- **Division access remains authoritative:** read-only access and shared views never broaden the records a user may read.
- **Database ownership:** schema changes for FE-05 and FE-07 belong only in `src/OF.Data.Design`.
- **Localisation and accessibility:** all new visible copy uses `react-i18next`, and every new picker/filter has a complete
  keyboard path and visible focus.

## Product assumptions to confirm

The specifications proceed with the following recommended assumptions so implementation can be planned. Change the
relevant specification before coding if any assumption is rejected.

1. A read-only user can customise a table for the current session and apply shared views, but cannot write, update,
   delete, or share a server-persisted view. Local browser fallback preferences remain acceptable.
2. The agreement warehouse remains visible in Availability even when it has no available stock; every other empty
   warehouse starts hidden and can be restored individually.
3. “Add columns” means restoring fields from the approved Agreement/Asset column catalogue, not exposing arbitrary
   database fields.
4. “Last login” is the last successful NOF frontend session bootstrap, not an authoritative Microsoft Entra sign-in
   event. The Admin page labels this as **Last NOF access** to avoid overstating the audit meaning.
5. Named-view sharing is available to normal write-enabled users, but recipients are limited to configured NOF users
   visible through the caller's authorised divisions. Super Admins can select any configured user.

## Common definition of done

Each request is complete only when its own acceptance criteria pass and the implementation also:

- updates the Web API contract when an endpoint, persisted view, or authorisation rule changes;
- keeps old saved views and legacy records readable;
- includes focused frontend and backend tests proportional to the change;
- passes the affected production builds and `git diff --check`;
- has a desktop visual review plus keyboard, empty, loading, error, and narrow-layout checks where relevant; and
- leaves secrets, runtime state, generated vendor output, and machine-specific configuration untouched.

## Delivery record

All seven roadmap items, FE-01 through FE-07, are implemented on `release/NOF-New-Frontend`. The final combined changes
were merged in [PR #562](https://github.com/AggrekoTechnologyServices/Order-Fulfillment/pull/562) on 2 September 2026.
Automated frontend verification completed with 52 test files and 404 tests passing, together with the production build,
lint, scoped formatting, and `git diff --check`. The focused backend suite completed with 186 tests passing. Manual UAT
was confirmed complete by the product owner on 2 September 2026.

Azure release run `1.11.3` (`123197`) completed its Version/Build/Publish/Docs and Dev Deployment stages successfully.
The SIT, Test, and Live deployment stages remain governed by their normal release-management approvals.

The recommended FE-06 hidden-filter behaviour and FE-07 eligible-recipient rule below are accepted product decisions.
Any deployment promotion or environment-specific smoke testing is release management rather than outstanding roadmap
implementation.
