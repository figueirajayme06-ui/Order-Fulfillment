# Feature specification: non-serialised stock fulfilment

## 1. Objective

**User and job:** A fulfilment planner needs to reserve quantity-managed stock, including substitute items, from the
same agreement availability workbench used for individual assets.

**Problem:** Availability includes `ProductItems`, but selecting a quantity-managed cell performs an asset lookup and
ends with "No individual assets found".

**Outcome:** Selecting quantity-managed availability opens an inline quantity allocator. The server validates the
item, warehouse, period, and available stock before creating a legacy-compatible reservation.

## 2. Scope and boundaries

### In scope

- Distinguish individual-asset, quantity-managed, and generic-only availability rows.
- Show available stock in the existing expanded availability row.
- Let users enter the number of units of the selected stock item to reserve.
- Store that direct quantity in both `Reservation.Quantity` and `Reservation.EffectiveQuantity`.
- Consolidate matching quantity-managed reservations in the reservations column and show their total quantity.

### Out of scope

- Editing an existing quantity reservation in place; users can remove and recreate it.
- Changing external M3 stock quantities or adding a database column.
- A general stock-adjustment or shortage-override workflow.

### Constraints and known rules

- Non-serialised stock is represented by `ProductItems`, not `Assets`.
- A non-serialised reservation remains compatible with the legacy representation: `AssetId == ItemNumber`.
- Substitute selection does not imply a conversion ratio. The planner decides the appropriate stock quantity.
- NOF availability uses `dbo.GetFulfilmentAvailabilitySummary` and `dbo.GetFulfilmentGenericsWithSubstitutions`. The helper includes every configured generic substitution purpose but deliberately supplies no `NumberNeeded` or other conversion-ratio semantics.
- `dbo.GetCPQAvailabilitySummary` and `dbo.GetGenericsWithSubstitutions` remain CPQ Next-only compatibility contracts and are not extended with NOF-specific fields or rules.
- Quantity availability is clamped to zero after subtracting the peak quantity of simultaneously active unconfirmed reservations overlapping the inclusive requested period from `StockQuantity - AllocatedQuantity`. It does not sum disjoint pending reservations, and confirmed reservations are expected to be represented by `AllocatedQuantity` already.
- The API remains the division-access and validation boundary.
- The availability inspector is excluded from print and remains a bounded, keyboard-operable work surface.

## 3. User experience

### Primary flow

1. Select an agreement line and a quantity-managed availability cell.
2. Review the item, warehouse, period availability, and outstanding agreement-line quantity.
3. Enter the stock quantity to reserve.
4. Reserve stock and refresh agreement, reservation, and availability state.

### States and edge cases

- Loading: show a compact in-panel processing state and disable duplicate submission.
- Empty: quantity-managed cells do not perform an individual-asset lookup.
- Error: retain the entered quantity and show the server message for invalid or changed stock.
- Success: close the allocator and refresh the workbench.
- Disabled: prevent submission when the entered quantity is invalid or exceeds current stock availability.

### Accessibility and responsive behaviour

- Use a labelled numeric input and a native submit button.
- Announce submission errors and expose the selected quantity as text, not colour alone.
- Preserve the existing horizontally scrolling availability fallback and print exclusion.

## 4. Technical plan

- Affected components: `AvailabilityPanel`, `AgreementDetailPage`, availability and fulfilment services.
- API/data changes: availability rows gain `reservationMode`; add a validated non-serialised reservation command; use the NOF-specific availability procedure and substitution helper.
- Database ownership: `src/OF.Data.Design` is the sole deployable source of truth for the CPQ-compatible and NOF-specific procedure/function contracts in the shared database.
- Existing patterns: inline availability expansion, reservation refresh, semantic controls, theme tokens.
- Tasks:
  1. Extend availability metadata to distinguish asset and quantity reservation modes.
  2. Add the line-scoped stock command with access, quantity, and availability validation.
  3. Add the inline quantity allocator and localised copy.
  4. Add focused controller/component coverage and run builds.

## 5. Acceptance criteria

- [x] Quantity-managed cells show an allocator instead of an asset-empty state.
- [x] Users directly enter the quantity of the selected stock item to reserve.
- [x] The server rejects quantities that are invalid or exceed current period availability.
- [x] Saved reservations use the entered value for `Quantity` and `EffectiveQuantity`, with equal item/asset identifiers.
- [x] Matching quantity-managed reservations are shown once per item and warehouse with an aggregated quantity badge.
- [x] Quantity availability subtracts the peak overlapping pending-reservation quantity without double-counting confirmed allocations.
- [x] Individual-asset selection and zero-availability repair inspection continue to work.

## 6. Verification

- Focused tests: availability mapping, stock reservation validation, AvailabilityPanel quantity interaction,
  reservation grouping, and AgreementDetail refresh.
- Commands: focused Vitest and .NET tests, frontend build, and `git diff --check`.
- Manual: exact item, mixed substitutes, insufficient stock, stale stock, keyboard submission, and asset selection.

## 7. Decisions and open questions

| Item | Decision or question | Owner / resolution |
| --- | --- | --- |
| Quantity meaning | The entered stock quantity is stored in both `Quantity` and `EffectiveQuantity`. | Legacy-compatible contract |
| Shortage override | Do not permit it in this workflow; return a conflict after rechecking stock. | Safer initial behaviour |
| Substitute ratios | Do not calculate or restrict ratios; the planner enters the required quantity. | Product decision |
| Availability SQL | Use the NOF-specific procedure/helper and preserve the CPQ-only compatibility contract. | `OF.Data.Design` owns deployment |
