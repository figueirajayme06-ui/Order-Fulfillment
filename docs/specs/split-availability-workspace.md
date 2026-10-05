# Split availability workspace

## 1. Summary

Provide fleet planners with a persistent line queue beside the availability workspace so they can work through an agreement without repeatedly opening and closing line context.

## 2. Problem

The vertical agreement-lines/availability split gives both areas only part of the viewport height. Copying the full agreement table into a horizontal split would also waste width on fields that are not needed while choosing stock.

## 3. Users and outcome

- Primary user: fleet planner fulfilling multiple agreement lines.
- Outcome: move quickly between lines while keeping the selected line's warehouse availability visible and usable.

## 4. Scope

### In scope

- Keep the full agreement table before a line is selected.
- Replace it with a compact line queue when the availability workspace opens.
- Show line number, item, quantity, warehouse, reservation count, and fulfilment state in the queue.
- Place the line queue on the left and availability on the right.
- Make the divider pointer- and keyboard-resizable.
- Preserve the selected row, filters, availability interactions, reservation actions, loading states, and print output.
- Stack the queue above availability on constrained viewports.

### Out of scope

- API or availability calculation changes.
- Reservation-rule changes.
- Redesigning individual asset details inside the availability matrix.

## 5. Interaction

1. Selecting a line turns the agreement area into a master/detail workspace.
2. The selected queue entry is highlighted and availability opens on the right.
3. Selecting another queue entry replaces availability without leaving the workspace.
4. Close returns to the full agreement table and restores focus to the selected line.
5. The divider defaults to 32% queue / 68% availability and can allocate 55–80% to availability.

## 6. Acceptance criteria

- Multiple agreement lines remain visible and selectable while availability is open.
- The availability pane receives at least 55% of desktop workspace width.
- The compact queue does not reproduce low-value date/action columns from the full table.
- The active line and its fulfilment state are visually and programmatically identifiable.
- Keyboard users can resize the panes and switch lines.
- The original full agreement table remains unchanged outside the focused workspace and in print.

## 7. Verification

- Component tests for opening, switching, closing, line context, and divider resizing.
- Frontend lint, full tests, production build, and scoped formatting.
- Visual review against a representative multi-line agreement.
