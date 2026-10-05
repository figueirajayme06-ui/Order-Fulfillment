# Reservation timeline parity

## Objective and scope

Planners can read reservation planning dates and identify every reservation beneath its owning line or asset in the React detail and Asset timelines. Agreement-list aggregate bars retain their current meaning. Existing timeline controls, saved views, print hooks and Frappe Gantt remain in use; no dependency, vendor, schema or mutation changes are required.

## Verified legacy rules

- `src/OF.UI/wwwroot/js/site/tasksGrid.js`, `generateAssetEventsBar`: sorts by start date and appends a separate row for **every** event under one asset context row. Adjacent and non-overlapping events do not share a lane. Each day, including both endpoints, is occupied. Full original dates appear in both the label and title.
- `src/OF.Data.Design/dbo/Stored Procedures/GenerateEvents.sql`, RESERVED events: start is delivery date then valid-from; end is collection date then termination then valid-to. Reservations inherit the owning line's period, including depot/rehire records on the detail timeline.
- Existing agreement detail API already exposes collection date; the frontend type is updated to consume that field.

## Experience and implementation

Detail tasks are grouped as a line followed immediately by its reservations, sorted by stable reservation ID. Asset events sort by start date, preserving response order for ties (as in the legacy UI); content-based IDs disambiguate duplicate occurrences and remain stable when earlier events are inserted. One asset context row has the combined height of all of that asset's event lanes, showing one asset link and one selection checkbox. Every event retains its own 38px lane. Empty assets keep one context lane with no visible bar.

The shared renderer shows formatted dates in bar labels and accessible names. Focusing a bar exposes the same original period in a visible details region; pointer hover exposes it too. Enter/Space retains any existing task action. Date text comes from unmodified source metadata, even when geometry is clipped at the history boundary or ten-year renderer limit. Dates use the calendar-date prefix from the API rather than timezone conversion. Same-day events occupy exactly one inclusive day. Missing start/end values are labelled unknown/open-ended; rendering fallbacks are never presented as source dates. Invalid/reversed intervals are labelled unavailable and kept visible at the known start for inspection.

Loading, missing agreements and errors retain a recoverable page state. Existing period controls, legends, context resizing, saved-view filters and print hooks remain. The detail view gains a visible period label and reservation legend. A per-event lane intentionally grows with reservation count, matching the legacy manifest-like timeline; the compact agreement grid remains the operational workspace for very large line quantities.

## Acceptance and verification

- Verify date precedence, date-only/timezone input, same-day, missing/open-ended, reversed and clipped periods with focused model tests.
- Verify grouping preserves overlaps, nested/identical/adjacent intervals, selection count and empty asset rows.
- Verify keyboard focus exposes dates and escapes unsafe task strings in Frappe popup HTML.
- Exercise hundreds of lines and 1,000 reservations in pure model coverage; no browser performance guarantee is inferred from model timing.
- Run focused timeline tests, production build, then integrator's full frontend/lint/format checks.
- Browser checks cover period navigation, context alignment/resizing, legend, keyboard, empty assets and print; authenticated live original/new UI comparison remains environment dependent. Legacy source verification above is authoritative for implementation decisions, not a claim of a live UI comparison.

## Delivered verification

- 43 focused tests across seven timeline/model/component suites passed, including 400 lines and 1,000 reservations in the pure detail model. Production build passed (existing bundle-size warning).
- Headless Microsoft Edge at 1440 x 1000: three assets with overlapping/nested/adjacent reservations produced three context rows and four visible event bars. Context heights were 114px/38px/38px; each bar aligned 7px below its lane top. The empty asset had no visible event bar.
- Keyboard focus on a one-day bar exposed its full original period; Earlier/Later and keyboard context resizing operated without page errors.
- Print-media review initially exposed an existing header-spacer mismatch when filters disappear. The component now realigns on print-media/beforeprint/afterprint changes. Recheck aligned all printed rows: 114px group, then 38px rows, every bar 7px below its corresponding lane top. Date labels and legend remained visible; interactive controls were hidden.
- This was an isolated component fixture with real browser rendering, not an authenticated production-data comparison. The integrator covers agreement timeline and mutation workflows separately.
