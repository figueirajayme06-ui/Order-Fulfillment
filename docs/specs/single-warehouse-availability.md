# Single-warehouse availability view

## Design note

- **User and job:** A planner needs to scan every stock option for the selected agreement line at one warehouse without opening each item separately.
- **Existing pattern:** Availability's View options menu, searchable warehouse selector, item summaries, asset commitments and quantity allocator.
- **Information hierarchy:** Keep the selected line, attributes and reservations above a bounded scrolling work surface. Show one compact warehouse table with an asset, item number, description, location, commitments and reservation action on each row. Display the warehouse once above the table and the date range once in the commitments header. Omit stock-count summary rows and repeated item panels. Keep unavailable stock visible for planning. Use the existing pale green and red availability colours on rows, with a compact check or cross beside the asset ID so colour is never the only signal. Expose the full status as an accessible label and tooltip.
- **States:** Default to the agreement warehouse, or the first warehouse if unavailable. Empty warehouses show the existing no-stock message. Each item loads independently with asset and schedule error/retry states. Read-only users can inspect stock but cannot reserve it.
- **Interaction:** Place View options before the location controls so users choose the presentation before its divisions and warehouse. Enable “Single warehouse — all stock options” there. Choose one warehouse from the selected divisions, including warehouses hidden in the summary. Summary facility/warehouse visibility preferences are retained when switching back. Use existing keyboard-accessible controls and horizontal overflow on narrow surfaces. The agreement's existing print workflow excludes the availability inspector.
- **Acceptance:** Only the selected warehouse appears; all its stock options appear as rows under one shared header; each asset clearly shows whether it is available for the whole selected period; serialized assets are fetched without the summary's 20-asset limit; quantity stock remains allocatable; warehouse, line and date changes cannot show stale results; returning to summary restores its filters.

## Scope and verification

No API, database, dependency, deployment or reservation contract changes. The existing division-scoped summary response is filtered on the client; asset requests target only the selected warehouse. The view choice is retained while the availability panel remains mounted.

Verify focused availability and locale tests, production build, lint and formatting. Review the desktop layout with real agreement data and retain the existing print exclusion. Tests cover more than 20 assets, warehouse switching, returning to summary, quantity stock, and read-only reservation protection.
