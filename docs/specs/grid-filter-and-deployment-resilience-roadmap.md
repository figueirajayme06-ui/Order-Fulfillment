# Grid filtering and deployment resilience roadmap

> **Status:** In progress
> **Created:** 3 September 2026
> **Scope:** Current React frontend and `OF.WebApp` host only

## Purpose

This roadmap covers seven related usability and release-resilience requests:

1. Filter a grid column by multiple values, such as several asset IDs.
2. Restore a user's grid context after they open a record and return to the list.
3. Ensure a deployment serves the new frontend reliably while preserving NOF sessions and working grid context.
4. Correct the alignment of the Alerts count and panel-header actions.
5. Replace the separate ringfence search and select controls with one simple searchable picker.
6. Keep saved-view recipient selection compact and prevent recipients from resharing a received view.
7. Show which commit and release is deployed to each test environment without exposing the display in Live.

The first two items share the Agreement and Asset saved-state model and should be delivered together. The deployment
item is independent and can be released separately.

## Proposed backlog

| ID    | Enhancement                               | Priority | Size / risk     | Outcome                                                                                                   |
| ----- | ----------------------------------------- | -------- | --------------- | --------------------------------------------------------------------------------------------------------- |
| FE-08 | Multi-value column filters                | P1       | Medium / medium | A user can paste or enter several values and see rows matching any value in that column.                  |
| FE-09 | Restore transient grid context            | P1       | Medium / low    | Opening a record and returning does not discard filters, sort, view, page, or page size.                  |
| FE-10 | Deployment cache resilience               | P1       | Medium / medium | New releases load current HTML/assets without clearing authentication or session-scoped working state.   |
| FE-11 | Alerts alignment fixes                    | P2       | Small / low     | The unread count and panel-header actions are visually centred and consistent.                            |
| FE-12 | Searchable ringfence picker               | P1       | Medium / low    | Selecting a target is one compact search-and-select interaction.                                          |
| FE-13 | Compact, owner-controlled view sharing    | P1       | Medium / medium | Recipient search no longer overtakes the grid, and received views cannot be reshared.                     |
| FE-14 | Non-production deployment identity        | P1       | Medium / low    | Testers can identify the exact release and commit currently running in Dev, SIT, and Test.                |

## FE-08: Multi-value column filters

### User outcome

Fleet planners can investigate a known set of requests or assets in one operation instead of repeating the same
single-value filter. For example, filtering Asset ID by `ASSET-101`, `ASSET-204`, and `ASSET-319` returns all three
matching assets.

### Interaction and matching rules

- Add multi-value entry to free-text column filters on Agreement and Asset table and timeline views.
- Filter immediately from the active text. Enter commits that text as one or more removable tokens.
- Accept values entered individually and values pasted as comma-, semicolon-, or newline-separated text; separators do
  not commit tokens until Enter is pressed.
- Display committed values as removable tokens, with a clear-all action and an accessible label containing the column
  name.
- Combine values within one column using **OR**. Continue combining different active columns using **AND**.
- Match every free-text token case-insensitively as a partial value after trimming, including identifier fields such as
  Asset ID and Agreement Number.
- Do not change date, status, warehouse, division, or other select filters into token inputs.
- Deduplicate values case-insensitively and ignore empty values. Cap a column at 100 values and show a localised
  validation message when a paste exceeds the cap.

### Technical tasks

1. Introduce a serialisable multi-value text-filter model and parser beside `TableColumnControls`; keep parsing and
   matching as pure functions.
2. Extend the shared text column control with an opt-in multi-value mode. Existing single-value consumers remain
   unchanged.
3. Update Agreement and Asset table/timeline filtering to apply partial matching to every free-text token.
4. Advance saved-view state to a new backward-compatible version. Decode existing string filters as one-value arrays
   and continue accepting saved state versions 1 through 4.
5. Add focused parser, model, saved-view migration, component keyboard, and page integration tests.

### Acceptance criteria

- [x] Entering `ZAD, YCK` into Asset ID shows identifiers containing either fragment.
- [ ] Multiple values in one column use OR; active filters in separate columns still use AND.
- [x] Active text filters immediately; Enter commits tokens, while Backspace and the remove controls remain keyboard
      operable.
- [ ] Duplicate, blank, and differently-cased duplicate values do not create duplicate tokens.
- [ ] Reset filters clears every token, and saved views round-trip multi-value filters.
- [ ] Existing saved views containing string column filters still load with the same result.

## FE-09: Restore transient grid context

### User outcome

When a user opens an Agreement or Asset profile and returns to its list, or leaves and returns to another grid during
the same browser tab session, each grid reopens in the context they were using.

### Persistence rules

- Automatically store the working list state in `sessionStorage`, separately for Agreements, Assets, Ringfence, and
  Admin and scoped by the signed-in user's stable identifier.
- Persist view mode, basic filters, column/date/timeline filters, column layout, sort, current page, and page size.
- Do not persist loaded rows, selected assets, dialogs, errors, loading state, or Ringfence hand-off context.
- Restore automatic state only when the page does not have an explicit saved-view selection or route hand-off state.
- An explicitly selected saved view wins and replaces the automatic working state.
- Reset filters clears both the visible filters and their automatic cached values. It does not delete named saved views.
- Validate cached data through the same saved-state decoder used by saved views. Ignore malformed, unknown-user, or
  unsupported-version data without blocking page load.
- `sessionStorage` deliberately limits restoration to the current browser tab/session. Long-lived, cross-device state
  remains the responsibility of named saved views.

### Technical tasks

1. Extract the existing Agreement and Asset state capture/apply operations into reusable page-state functions and
  apply the same storage contract to both Ringfence grids and the Admin user grid.
2. Add a small versioned session-state storage helper with guarded browser storage access and per-user/page keys.
3. Lazily initialise each list page from valid session state before its first data request, avoiding a default-state
   request followed by a restored-state request.
4. Persist state after changes with a short debounce and restore/clamp pagination after filtered row counts are known.
5. Add tests for navigation away/back, refresh, reset, malformed storage, user isolation, saved-view precedence, and
   unavailable storage.

### Acceptance criteria

- [x] Filters survive Agreement/Asset list to detail and browser Back navigation, including active text drafts.
- [x] Agreement, Asset, Ringfence, and Admin contexts do not overwrite each other or leak between signed-in users.
- [ ] Sort, view mode, page size, and a valid current page are restored without duplicate API requests.
- [ ] Reset filters remains reset after leaving and returning.
- [ ] Applying a named saved view takes precedence over automatic cached state.
- [ ] Closing the tab/session removes the automatic context; named saved views remain available.

## FE-10: Deployment cache resilience

### Feasibility decision

A deployment cannot proactively delete cookies from browsers that are not making a request. `OF.WebApp` also does not
own the authentication cookie: Azure App Service Easy Auth authenticates the request before the ASP.NET Core pipeline
and supplies identity headers.

The supported outcomes are:

1. **Cache freshness:** make every browser discover the new HTML while retaining immutable, content-hashed Vite
   assets.
2. **Every deployment:** preserve authenticated sessions and FE-09 working state; frontend cache freshness must not
   require signing users out or clearing browser storage.

Do not use `Clear-Site-Data: "cookies", "storage"` as the normal cache-busting mechanism. It has broad browser support
limitations, cannot reach inactive clients, and would erase useful NOF local/session state and saved-view fallbacks.

### Technical tasks

1. Confirm Vite's hashed asset output in the container build and add host cache policy tests:
   - `index.html` and SPA fallback responses: `Cache-Control: no-store`.
   - fingerprinted JS/CSS/assets: `Cache-Control: public, max-age=31536000, immutable`.
   - API responses containing user or operational data: no shared/public caching.
2. Expose the deployed `app_version` to `OF.WebApp` as configuration and return it from a lightweight version endpoint
   or response header. Include it in logs and health/deployment smoke checks.
3. Add frontend stale-release handling. On a version mismatch or failed dynamic chunk import, perform one guarded hard
   reload; prevent reload loops and preserve FE-09 state.

### Acceptance criteria

- [ ] A browser with a previously cached release receives current `index.html` after deployment.
- [ ] Content-hashed assets are cached immutably and old assets remain available during the deployment transition.
- [ ] An ordinary deployment does not sign users out or remove saved filters/views.
- [ ] Deployment telemetry identifies frontend/API version mismatches and guarded stale-release reloads.
- [ ] Rollback to the previous image preserves authentication and session-scoped working state.

## FE-11: Alerts alignment fixes

### User outcome

The Alerts entry and its open panel read as deliberate, stable controls: the unread count is centred in its badge, and
Refresh and Close share the same vertical alignment in the header.

### Technical tasks

1. Give the sidebar badge a fixed minimum geometry and centre its content on both axes for one digit, two digits, and
   `99+`. Avoid baseline-dependent positioning and preserve the compact-sidebar overlay position.
2. Replace the text multiplication-sign Close control with the existing Lucide close icon, retaining the accessible
   `Close alerts` label and a stable square hit target.
3. Align the Refresh and Close controls through the header action container rather than per-button offsets. Apply the
   same header treatment to Data refreshes so the shared dropdown pattern remains consistent.
4. Add component assertions for labels and icon controls, then visually check expanded and compact sidebars at 100%
   and 200% zoom.

### Acceptance criteria

- [ ] Alert counts `1`, `12`, and `99+` are horizontally and vertically centred without changing sidebar row height.
- [ ] Refresh and Close are vertically centred, have stable hit areas, and do not move when the panel content changes.
- [ ] Alerts and Data refreshes use the same header-action alignment.
- [ ] Both controls remain keyboard operable with visible focus and accessible names.

## FE-12: Searchable ringfence picker

### User outcome

A user can find and select a target ringfence from one control instead of typing in a search field and then moving to
a separate dropdown.

### Interaction rules

- Replace the standalone search input and native select with one accessible searchable combobox.
- The closed control displays `Select a ringfence` or the selected ringfence name.
- Opening the control focuses its search input. Results filter by ringfence name as the user types.
- Arrow keys move through results, Enter selects, Escape closes, and Tab follows normal focus order.
- Show a compact no-results message only after the user enters text. Do not clear a valid selected target merely
  because the current query does not match it.
- Keep contextual Ringfence-to-Assets targets locked and displayed as they are today; they do not need search.
- Do not add a new combobox dependency without team approval. First assess whether an existing shared control or a
  small page-specific semantic implementation meets the keyboard and screen-reader requirements.

### Technical tasks

1. Add the searchable picker within `AssetRingfenceActions`, preserving the existing selected ID contract and disabled
   states.
2. Remove the duplicated search field, clear-search button, and filtered native-select behavior.
3. Keep one primary `Add to Ringfence` action in the persistent toolbar. Review the selected-assets summary so it does
   not duplicate the same Add command unless user testing demonstrates a need for both.
4. Add focused interaction tests for mouse selection, keyboard selection, no results, retained selection, busy state,
   contextual target, and a long ringfence list.

### Acceptance criteria

- [ ] Search and selection happen in one labelled control without horizontal clipping at supported widths.
- [ ] A ringfence can be found and selected using keyboard only.
- [ ] Selecting a result closes the popup and immediately enables Add when assets are selected.
- [ ] No-match searches do not discard the previously selected target.
- [ ] Contextual and busy/locked states remain correct.

## FE-13: Compact, owner-controlled view sharing

### User outcome

Sharing a view is an intentional, compact task. The grid remains visible, candidate users appear only after searching,
and a user receiving someone else's view cannot alter or redistribute that view.

### Interaction and permission rules

- Keep recipient management collapsed by default. Choosing `Specific users` exposes a compact recipient search, not
  the complete candidate directory.
- Do not render candidate rows until the user enters search text. Remove the visible `N eligible people` count and do
  not show the first users by default.
- Return a bounded result list, initially recommended at 10 matches, ordered by best name/login match. Keep selected
  recipients visible as removable tokens for the owner.
- Show the recipient container only while creating a specific-users view or while the owner/admin explicitly edits
  sharing for an editable view. Collapse it after save, cancel, or view selection.
- A received `Shared with me` view is read-only: hide recipient data, disable update/delete/scope changes, and do not
  permit Save New to inherit its `users` scope or recipients.
- If copying a received view remains useful, expose an explicit `Save a copy` action that starts a new **personal**
  view with no recipients. It must not silently reshare the original owner's state.
- Enforce ownership on the API as well as in the UI. An administrator may retain the existing management override;
  ordinary recipients cannot update sharing metadata on the source view.

### Technical tasks

1. Change `SavedViewRecipientPicker` to an idle/search/results model with no initial directory rows or total count and
   a fixed-height bounded popup.
2. Add explicit sharing-edit state to Agreement and Asset saved-view controls rather than deriving permanent picker
   visibility solely from `scope === "users"`.
3. When selecting a received view, preserve `isSelectedViewReceived`, prevent inherited sharing fields from reaching
   create/update requests, and provide the personal-copy path if approved.
4. Confirm recipient candidate filtering can be bounded server-side. Add an optional search query and result limit to
   the API if loading the full eligible directory is unnecessary or does not scale.
5. Add matching Agreement/Asset component, hook, service, controller authorization, and API contract tests.

### Acceptance criteria

- [ ] Selecting `Specific users` does not display the first candidate users or an eligible-person total.
- [ ] Candidate rows appear only after searching and remain height-bounded without pushing the grid off screen.
- [ ] Selected recipients remain visible and removable while the owner is editing sharing.
- [ ] Selecting a received view never displays its recipient editor and cannot update, delete, or reshare that view.
- [ ] Saving a copy of a received view, if enabled, defaults to personal scope with zero recipients.
- [ ] The server rejects unauthorized recipient/sharing changes even if the client request is manipulated.

## FE-14: Non-production deployment identity

### User outcome

Testers and developers can tell which NOF release and source commit is currently running in an environment without
asking the delivery team or inferring it from browser assets. The display is available in Dev, SIT, and Test and is
absent in Production/Live.

### Reference pattern and boundaries

- Reuse the CPQ pattern: the deployment supplies immutable build metadata, an authenticated API returns it, and the
  frontend presents a compact version/commit indicator with a link to the repository commit.
- CPQ's current implementation identifies the running build; it is not an application-owned release-history store.
  Azure DevOps remains the source of truth for prior releases and deployment history.
- Show only deployment metadata: environment, release/build number, source ref or release tag, commit SHA, and build or
  deployment timestamp. Do not expose pipeline credentials, internal service-connection details, or configuration.
- The feature must be controlled by an explicit `ShowDeploymentInfo` server configuration value. Set it to true only
  for Dev, SIT, and Test and false for Live. Do not infer safety solely from a user-editable label.
- Apply defence in depth: when disabled, omit deployment fields from `api/app-config` (or return `404` from a dedicated
  endpoint), and do not render or request the deployment-info UI in the frontend.

### Experience

- Add a compact, subdued deployment indicator to the existing non-production preview area or application footer. It
  must not compete with operational controls or reduce grid space materially.
- Display the environment and release first, followed by a shortened commit SHA. Make the commit a link to the
  corresponding `Order-Fulfillment` GitHub commit using a fixed repository base URL and a validated hexadecimal SHA.
- Expose the full commit SHA, source ref, and timestamp through accessible text or a small details disclosure.
- Provide neutral loading and unavailable states in test environments only; failure to load metadata must never block
  the application.
- Do not display a historical release list in NOF for the initial delivery. If testers need history, provide a link to
  the relevant Azure DevOps environment/deployment history subject to normal access permissions.

### Technical tasks

1. Extend the build/deploy pipeline to pass `Build.SourceVersion`, the generated release/version number, source ref,
   and an ISO 8601 build timestamp into the deployed Web App as immutable app settings or container environment
   variables. The existing container image already uses `Build.SourceVersion` as a tag.
2. Extend `FrontendExperienceOptions` and the app-configuration response, or add a no-store authenticated build-info
   endpoint modelled on CPQ's `/api/BuildInfo`. Keep naming consistent with NOF's existing `api/app-config` contract.
3. Configure `ShowDeploymentInfo=true` for Dev, SIT, and Test and explicitly set it to false for Live in Terraform or
   environment-specific pipeline configuration. Document the setting in the Web API and release documentation.
4. Add a localised frontend deployment-info component to the preview banner/footer and validate commit links before
   rendering them. Do not fetch the endpoint when the feature flag is false.
5. Add pipeline/configuration, controller, frontend service/component, accessibility, and negative Live-environment
   tests. Include the reported release and commit in deployment smoke-test output for non-production environments.

### Acceptance criteria

- [ ] Dev, SIT, and Test identify the currently running environment, release/build number, and commit SHA.
- [ ] A valid commit SHA links to the matching `AggrekoTechnologyServices/Order-Fulfillment` commit in a new tab with
      safe external-link attributes.
- [ ] Full metadata is accessible without permanently occupying significant grid space.
- [ ] Missing or malformed metadata produces a non-blocking unavailable state and never an unsafe link.
- [ ] Live renders no deployment indicator, makes no frontend build-info request, and its public app-config response
      contains no deployment metadata.
- [ ] The displayed release and commit match the immutable container tag/pipeline values verified by the deployment
      smoke test.

## Delivery order

1. **FE-08 model and compatibility:** agree matching semantics, add pure model/parser tests, then migrate saved-state
   decoding.
2. **FE-08 user interface:** add the token control and connect Agreement/Asset table and timeline views.
3. **FE-09 restoration:** reuse the upgraded state model for automatic session persistence and navigation tests.
4. **FE-10 cache policy:** ship and verify cache headers/version observability without changing authentication sessions.
5. **FE-11 Alerts alignment:** deliver independently as a focused visual correction.
6. **FE-12 ringfence picker:** replace the paired controls and validate keyboard behavior before visual sign-off.
7. **FE-13 view sharing:** implement the compact picker and ownership rules together so UI and API behavior agree.
8. **FE-14 deployment identity:** align with FE-10 version metadata, then wire the display and environment guards into
   the deployment pipeline.

FE-08 and FE-09 touch the same page state and tests and should not be implemented concurrently by separate agents.
FE-10 and FE-14 share version metadata and should use one API/configuration contract. FE-11 can proceed independently.
FE-12 touches the Asset toolbar also used by FE-09; coordinate those page edits. FE-13 shares saved-view state with
FE-08 and FE-09 and should follow them or be delivered by the same owner.

## Decisions required before implementation

| Decision                                                       | Recommendation                                                                                               | Owner                         |
| -------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ | ----------------------------- |
| Which text columns support multiple values?                    | All free-text table/timeline columns; every token uses partial matching.                                     | Product                       |
| How long should automatic filters survive?                     | Current tab/session only; use named saved views for durable state.                                           | Product                       |
| Should the selected-assets summary retain a second Add action? | Decided: no. Keep one persistent Add action beside the target picker and retain Clear in the summary.         | Product / UX                  |
| Can a recipient copy a received view?                          | Allow only an explicit personal copy with no inherited recipients; never allow direct resharing.             | Product / Security            |
| Should recipient search be server-side?                        | Use server-side bounded search if the eligible directory can grow beyond a small list.                       | Engineering                   |
| Which environments show deployment identity?                   | Dev, SIT, and Test only; explicitly disabled in Production/Live.                                             | Product / Release Management  |
| Where should prior deployment history live?                    | Keep Azure DevOps as the history source; NOF shows the currently running deployment and may link to history. | Release Management            |

## Verification and definition of done

- Run focused Vitest suites for table controls, Agreement/Asset models, saved-state parsing, storage, and navigation.
- Run `npm run build`, `npm run lint`, scoped formatting checks, affected `OF.WebApp` tests/build, and
  `git diff --check`.
- Manually verify desktop keyboard operation, pasted ID lists, empty/no-match results, browser Back, refresh, separate
  users, alert alignment, searchable ringfence selection, compact recipient search, received-view permissions, and
  narrow-layout fallback.
- Verify cache headers for `/`, a client-side fallback route, a hashed JS asset, and an authenticated API response.
- Exercise deployment transition and rollback with an old open tab, confirming authentication and FE-09 state remain
  intact.
- Verify deployment metadata against the Azure DevOps build and container tag in Dev, SIT, and Test, then assert the
  indicator, request, and response metadata are all absent in Live configuration.
