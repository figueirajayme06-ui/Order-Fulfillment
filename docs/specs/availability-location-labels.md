# Availability location labels

## Objective and scope

Make location choices easier to scan: warehouse options show code and name; facility options and grid headers show code and name when available.

## Contract and implementation

Add `facilityName` to the existing warehouse lookup response, sourced from `WarehouseItems.Facility`. Keep `facility` and all selection/grouping keys unchanged. The frontend treats the new field as optional, falls back to existing labels, and avoids repeating a name equal to its code. No schema, integration, permission, or workflow changes.

## Acceptance and verification

- Warehouse options omit division/facility prefixes and retain their existing unique values.
- Facility display names are scoped by division and facility code; missing names retain the current fallback.
- Existing searchable controls, keyboard behaviour, and print structure are preserved.
- Verify lookup serialization and frontend label/fallback tests, frontend build where permitted, and local visual review.
