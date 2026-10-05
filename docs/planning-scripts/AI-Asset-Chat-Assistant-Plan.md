# Future: AI Chat-Assisted Asset Selection

## Overview

This document outlines the plan for adding an AI chat assistant to the Asset Selector component on the order page, similar to the CPQ-Next `ChatControl` implementation used in `AddProductsSearch.tsx`.

The chat assistant will allow users to describe their asset needs in natural language (e.g. "I need a 500kW generator available in Glasgow") and have the system translate that into filter selections and search results.

## Reference Implementation (CPQ-Next)

The CPQ-Next `AddProductsSearch` component integrates an AI chat via:
- `CPQ.Frontend/src/components/chat/ChatControl.tsx` — Reusable chat UI with streaming responses
- `CPQ.Frontend/src/components/product/ProductSearchCommandHandler.ts` — Translates AI commands into filter state mutations
- Backend endpoint `/api/Chat/stream` — SSE-based streaming response from an LLM

### Key Patterns to Adopt

1. **Command-based architecture**: The LLM returns structured commands (JSON) that are executed by a command handler rather than directly manipulating state.
2. **Streaming responses**: Use `fetch` with SSE (Server-Sent Events) for real-time streaming of chat responses.
3. **Command handler pattern**: A dedicated class maps commands to state mutations (setting filters, triggering search, selecting assets).
4. **Contextual prompts**: The LLM receives the current filter state and available options as context.
5. **Initial question without LLM call**: Present the first interaction locally for instant UX.

## Implementation Plan

### Phase 1: Backend Chat Endpoint

Create a new endpoint in the NOF `OF.WebApp` project:

```
POST /api/chat/asset-search/stream
```

**Request body:**
```json
{
  "messages": [
    { "role": "user", "content": "I need a 500kW diesel generator in Edinburgh" }
  ],
  "context": {
    "currentFilters": { "warehouse": "EDI0", "division": "200" },
    "orderLine": { "itemNumber": "XGGN0500", "warehouse": "EDI0" }
  }
}
```

**Response:** SSE stream returning:
1. `presentQuestion` commands (for guided selection)
2. `setFilter` commands (to manipulate filter state)
3. `triggerSearch` commands
4. `selectAsset` commands (to auto-select a result)
5. Free-text explanations

### Phase 2: Frontend Chat Component

Create `src/components/fulfilment/AssetChatAssistant.tsx`:

```typescript
interface AssetChatAssistantProps {
  onCommand: (command: AssetChatCommand) => void;
  context: AssetSearchContext;
}
```

**Commands the LLM can issue:**
```typescript
type AssetChatCommand =
  | { command: "setFilter"; field: keyof AssetFilterValues; value: string }
  | { command: "clearFilters" }
  | { command: "triggerSearch" }
  | { command: "selectAsset"; assetId: string }
  | { command: "presentQuestion"; question: string; options: ChatOption[] };
```

### Phase 3: Command Handler

Create `src/components/fulfilment/AssetSearchCommandHandler.ts`:

```typescript
interface AssetSearchActions {
  setFilter: (field: keyof AssetFilterValues, value: string) => void;
  clearAllFilters: () => void;
  triggerSearch: () => void;
  selectAsset: (assetId: string) => void;
}

export class AssetSearchCommandHandler {
  constructor(private actions: AssetSearchActions) {}

  handleCommand(command: AssetChatCommand): void {
    switch (command.command) {
      case "setFilter":
        this.actions.setFilter(command.field, command.value);
        break;
      case "clearFilters":
        this.actions.clearAllFilters();
        break;
      case "triggerSearch":
        this.actions.triggerSearch();
        break;
      case "selectAsset":
        this.actions.selectAsset(command.assetId);
        break;
    }
  }
}
```

### Phase 4: Integration into AssetSelector

Add a toggle button (robot icon) in the `AssetSelector` header to show/hide the chat panel. The chat panel slides in from the right side of the modal (or bottom on mobile), similar to CPQ-Next's layout.

```tsx
// In AssetSelector.tsx
const [chatVisible, setChatVisible] = useState(false);

// In the header:
<button onClick={() => setChatVisible(!chatVisible)}>
  <BiBot /> AI Assistant
</button>

// In the modal body, alongside the filter bar:
{chatVisible && (
  <AssetChatAssistant
    onCommand={handleChatCommand}
    context={{ filters, orderLine: { itemNumber, warehouse, division } }}
  />
)}
```

## System Prompt Design

The LLM system prompt should include:
- Available asset statuses and their meanings
- The user's division and warehouse context
- Available filter fields and their valid values
- Instructions to return structured commands alongside natural language

Example system prompt snippet:
```
You are an assistant helping a fulfilment operator find the right asset to reserve against an order line.

Available commands:
- setFilter(field, value): Set a filter field. Fields: status, warehouse, division, itemNumber, description, search
- clearFilters(): Reset all filters
- triggerSearch(): Execute the search with current filters
- selectAsset(assetId): Select a specific asset from results

Context:
- Order line item: {itemNumber}
- Warehouse: {warehouse}
- Division: {division}
```

## Dependencies

- Azure OpenAI service (same as CPQ-Next uses)
- SSE streaming support (use `fetch` API, not axios — as per CPQ-Next pattern)
- The `AssetFilterBar` component (already implemented) provides the filter state that the AI can manipulate

## Integration with Existing AvailabilityPanel

The `AvailabilityPanel` component (`src/components/fulfilment/AvailabilityPanel.tsx`) already provides warehouse availability grids. The AI assistant could suggest using the availability panel for a given generic code, or could directly surface availability information in the chat response.

## Estimated Effort

| Phase | Scope | Estimate |
|-------|-------|----------|
| Phase 1 | Backend streaming endpoint + prompt engineering | Medium |
| Phase 2 | Frontend ChatControl component (port from CPQ-Next) | Medium |
| Phase 3 | Command handler + integration | Small |
| Phase 4 | UX polish, testing, error handling | Medium |

## Files to Create

```
src/OF.Frontend/src/components/fulfilment/AssetChatAssistant.tsx
src/OF.Frontend/src/components/fulfilment/AssetChatAssistant.module.css
src/OF.Frontend/src/components/fulfilment/AssetSearchCommandHandler.ts
src/OF.Frontend/src/types/assetChat.ts
src/OF.WebApp/Controllers/AssetChatController.cs
```

## Related CPQ-Next Files (Reference)

- `CPQ.Frontend/src/components/chat/ChatControl.tsx` — Full chat UI with streaming
- `CPQ.Frontend/src/components/product/ProductSearchCommandHandler.ts` — Command handler pattern
- `CPQ.Backend/Controllers/CPQ/ChatController.cs` — Backend streaming endpoint
- `CPQ.Backend/Prompts/` — System prompt templates
