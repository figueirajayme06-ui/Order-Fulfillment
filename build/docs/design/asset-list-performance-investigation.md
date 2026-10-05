# Assets list performance investigation — September 2026

## Observed dev baseline (24 September)

Measured `asofwebappdev` release `1.11.3` / `29a1e90` using `aiofdev` request and SQL dependency telemetry. The browser displayed 14,362 assets. These are server request durations, not browser rendering timings.

Comparable full-list queries (excluded removed/scrapped/sold, with either division `110,120` or no explicit division) showed a median of 2.14 seconds across seven earlier requests with two SQL dependencies, versus 4.73 seconds across 68 later requests with three dependencies. This is observational evidence: data, load, and deployment conditions were not controlled.

One 4.63-second request had SQL dependencies at 47 ms (7 ms duration), 67 ms (361 ms), and 3,498 ms (20 ms). The controller order suggests identity, asset list, and note counts respectively, but SQL text was not captured. The gap before the final dependency includes reading/materialising rows; it cannot be attributed entirely to change tracking. The latest explicit Refresh samples took 1.96 and 3.29 seconds.

Returning from Agreements to the saved All Assets view within the cache TTL reused its result but still issued an unnecessary initial `division=110,120` request before restoring the view.

## Targeted corrections

- Project the authorised asset query directly into `AssetListItemResponse` before `ToList`. This avoids full tracked entity materialisation introduced alongside note counting in `9e9f9ade`.
- Keep the existing grouped note-count query and attach counts to the projected rows. Preserve list fields, ordering, authorisation, and `take` behaviour.
- Wait for saved-view initialization before fetching assets. Refresh is also gated during initialization. The existing query cache remains in use after view restoration.
- Invalidate the page's pending result on effect cleanup so an obsolete or unmounted request cannot replace/cache current rows.

## Diagnostics and rollout verification

The asset endpoint now returns `Server-Timing` entries `assets`, `notes`, and `prepare`, in milliseconds. `assets` measures execution **and complete row reading/materialisation**. `notes` includes ID preparation and note-count retrieval; `prepare` measures attaching counts. These do not include authentication or response serialization/transmission.

Structured logs record row count and those phases. A response-completion log measures the response phase after preparation, including serialization and server-side response writing; it is not browser download/render time. Activity tags use `assets.row_count`, `assets.load_ms`, `assets.notes_ms`, and `assets.prepare_ms`. Activity-tag availability depends on telemetry collection configuration; the response header does not depend on log ingestion. No asset IDs, customer fields, note text, or credentials are logged by this instrumentation.

After deploying the corrected build:

1. Confirm the release badge changed from `29a1e90`.
2. Use the same saved All Assets view and record several explicit Refresh requests sequentially. Refresh bypasses the in-memory cache. Keep filters and row counts comparable; do not benchmark cached navigation as API time.
3. Inspect `Server-Timing` and the browser's waiting/download breakdown. Compare complete request durations in Application Insights as well as the individual load/notes phases.
4. Navigate Assets → Agreements → Assets within two minutes. Verify no asset-list request is made for either the initial divisions or the restored saved-view query.
5. Check note badges, a filtered list, empty results, and timeline switching. The API array contract and UI layout are unchanged.

The code correction is locally tested. A post-deployment comparison is still required before claiming a measured performance improvement. Pagination remains deferred pending this comparison.
