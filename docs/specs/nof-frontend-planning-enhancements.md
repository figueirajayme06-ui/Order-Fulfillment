# Feature specification: NOF frontend planning enhancements

## 1. Objective

Fleet planners need a more complete and reliable view of asset availability, Agreement commitments, Ringfences, and
operational events. The frontend must make this information easier to scan in tables and timelines without obscuring
the primary planning calendar.

## 2. Scope

### In scope

- Remove the status indicator from the Asset Status grid column while retaining the textual status value.
- Allow users to select additional approved context columns in Agreement and Asset timeline views.
- Provide ascending and descending sorting for every NOF grid view, including Agreements, Assets, Ringfences, and
  Administration.
- Replace bespoke Ringfence and Administration tables with the shared standard Grid component, preserving their
  current data, actions, filters, accessibility, and saved-view behaviour where applicable.
- Add a Ringfence timeline view that displays Ringfence periods alongside relevant asset events and makes overlapping
  Ringfences and events visually identifiable.
- Move Customer Number from the Agreement and Asset headers into a selectable grid/timeline column.
- Extend each calendar's visible planning range through the latest displayed Agreement end date plus 12 months.
- Add inclusive date-range filtering to Agreement and Asset table and timeline views so users can identify fleet
  availability within a selected period.
- Keep timeline scale selection stable while data, filters, columns, and viewport dimensions update.
- Make an Asset's Ringfence assignment visible at a glance in Asset list and timeline views.

### Out of scope

- Changing Ringfence overlap validation, assignment rules, or division authorisation.
- Changing Agreement, Asset, event, or Ringfence source data without a separate API/data-contract decision.
- Replacing the shared Grid component or adding a third-party grid dependency.

## 3. Functional requirements

### Asset status presentation

1. The Asset Status column must render the existing textual status value without a separate visual status indicator.
2. Status must remain available for filtering, sorting, export/print where currently supported, and accessibility.

### Grid consistency and sorting

1. Every grid must expose sorting for each sortable displayed column through keyboard-accessible header controls.
2. Users must be able to switch between ascending and descending order and clear the active sort.
3. Sorting must preserve active filters, pagination, selection, and authorised division scope.
4. Ringfence and Administration views must use the shared Grid component and its standard loading, empty, error,
   responsive, focus, and sorting behaviours.

### Customer number placement

1. Customer Number must not appear in the primary header area of Agreement or Asset views.
2. Customer Number must be available as an approved, selectable column in Agreement and Asset table and timeline
   context layouts.
3. Existing saved views that reference Customer Number must remain readable and be repaired to the current column
   model where required.

### Agreement and Asset date-range filtering

1. Agreement and Asset table and timeline views must provide a start date and end date filter using ISO dates.
2. A record is included when its relevant commitment, availability, or event period overlaps the inclusive selected
   range; a missing start or end date represents an open boundary.
3. The selected range must apply consistently to the grid rows, timeline lanes, event bars, counts, and empty state.
4. The frontend must preserve the current date range when switching between table and timeline modes and when a saved
   view is restored where the saved-view contract supports it.
5. Date filtering must not bypass server-side division access or imply that an Asset is available when its current
   status, reservation, Ringfence, or other authorised schedule data blocks it.

### Calendar range and scale stability

1. A timeline calendar must extend through the latest displayed Agreement end date plus 12 calendar months.
2. When no displayed Agreement has an end date, the existing default planning horizon remains in effect.
3. Timeline scale selection must remain unchanged when rows load, filters change, columns are configured, or the
   component resizes, unless the selected scale cannot represent the selected date range.
4. When a scale change is necessary, it must be explicit, predictable, and reflected in the date headers and event-bar
   geometry in the same render.

### Ringfence timeline

1. The Ringfences page must offer a timeline view in addition to its grid view.
2. The timeline must show each accessible Ringfence as a dated lane using its title, period, and relevant asset context.
3. The timeline must show relevant asset events in the same planning period using a distinguishable visual treatment.
4. Overlapping Ringfences must be visually identifiable without relying on colour alone.
5. An event overlapping a Ringfence must be visually identifiable and expose accessible text describing the overlap.
6. Timeline data, filters, sorting/order, division scope, loading, empty, error, keyboard, and print behaviours must
   follow existing NOF timeline conventions.

### Ringfence visibility on Assets

1. Asset table and timeline views must provide an at-a-glance Ringfence state for each Asset.
2. The state must distinguish no assignment, an active/current assignment, and an assignment outside the selected
   planning range.
3. Where an Asset has overlapping Ringfence assignments, the UI must indicate that multiple assignments exist and
   provide enough accessible detail to identify the applicable Ringfence records.
4. The indicator must not be the sole carrier of information; text and accessible names must communicate the state.

## 4. Acceptance criteria

- [ ] Asset Status shows text only, without a status indicator, and retains its existing operational behaviour.
- [ ] Every grid view supports accessible ascending, descending, and cleared sorting for its supported columns.
- [ ] Ringfence and Administration use the shared Grid component with no regression in their existing actions or data.
- [ ] Customer Number is removed from Agreement and Asset headers and can be selected as a table or timeline context
  column.
- [ ] Agreement and Asset date ranges filter both modes consistently using inclusive overlap semantics.
- [ ] Calendar coverage reaches the latest displayed Agreement end date plus 12 months.
- [ ] Changing data, filters, columns, or width does not reset a valid user-selected timeline scale.
- [ ] Ringfences has a timeline that identifies Ringfence/Ringfence and Ringfence/event overlaps accessibly.
- [ ] Asset list and timeline views clearly communicate each Asset's Ringfence state and multiple assignments.
- [ ] Focused component/service tests, the affected frontend build, and visual checks for wide and narrow desktop
  timelines pass.

## 5. Decisions to confirm before implementation

1. Confirm the approved additional timeline-column catalogues and visible-column limits for Agreements and Assets.
2. Confirm whether grid sorting is client-side, server-side, or hybrid for each paginated source.
3. Confirm which asset-event types appear on the Ringfence timeline and whether events outside Ringfence dates are
   included as contextual lanes.
4. Confirm the exact overlap rule used by date filtering for Assets that have multiple commitments or open-ended dates.
5. Confirm whether Ringfence assignment detail is supplied by existing Asset profile/list contracts or requires an
   additive API response field.