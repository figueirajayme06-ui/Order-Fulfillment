# DataGrid foundation

These components are the reusable, domain-neutral part of the Order Fulfillment grids.

- `DataGridColumnGroup` keeps visible column widths and print proportions aligned across table sections.
- `DataGridColumnHeaders` renders semantic, resizable header cells while the consumer supplies labels and header
  content.
- `DataGridPagination` renders controlled paging and optional page-size controls using consumer-supplied copy.

Consumers remain responsible for data fetching, row keys, cell and filter rendering, sorting, selection rules, empty
states, saved-view persistence, and any surrounding table or timeline layout. This is intentional: those
responsibilities contain application and domain policy.

## Future package contract

Before publishing these primitives for another application:

1. Validate the API against that application's real grid requirements.
2. Make React a peer dependency.
3. Replace direct theme-token assumptions with a documented theme adapter or token fallback contract.
4. Provide icon and translation adapters for the adjacent Columns and filter controls.
5. Version serialized column layouts independently from any consuming application's saved-view schema.
