# NOF asset location, telemetry, and service data: database findings

Investigated: 21 September 2026. Status: verified read-only discovery and initial application implementation;
no database changes, credential changes, or deployment.

Related: [frontend feasibility and requirements](../../../docs/specs/asset-location-telemetry-investigation.md).

## Recommendation

Use the existing MDP SQL Server integration infrastructure to add last reported GPS location, service
history, and retrofit status to NOF asset profiles. These data sources were found and queried successfully.
Keep rich live telemetry and calculated next-service due dates out of the initial scope: the inspected
database does not yet establish a reliable source/definition for those capabilities.

The target is `sqlsfdplv.database.windows.net`, database `fdplv-db`. The user identified it as the data
source behind `https://ai.aggreko.biz/asset-view`. The source website's code/network calls were not inspected,
so exact parity with that site's implementation is not established. The two example asset IDs and their
warehouse/location codes match the database records.

## Connection issue resolved for investigation

The credential supplied separately by the user successfully connected. The password parsed from the
`kv-ofdev` `MDPConnectionString` secret differs from that working credential. Explicit UTF-8 secret
retrieval still produced a login failure. Server, database, login name, encryption, and certificate-trust
settings matched the working connection. This explains the different outcomes without assuming a firewall
or general SQL connectivity problem. The vault was not changed.

Before runtime validation or deployment, establish the correct environment configuration and a supported read-only identity
for the required objects. Successful investigation with the supplied administrative login does not establish
least-privilege application access or the deployed warehouse job's current health. No credentials are included
in these documents.

## Source map

| Capability | Source and join | Assessment |
| --- | --- | --- |
| Asset master/context | `dbo.ProductHierarchy.IndividualItemNumber` matches NOF individual item number | Verified for both examples; includes manufacturer, serial, product hierarchy, warehouse/location, telemetry fitment and BOM fields. Reuse NOF's existing master fields where already adequate. |
| Equipment identity | `dbo.sitewatch_config_equipment.GPN` -> equipment `UID` and `COLLECTION_UID` | Verified for `100111NZ`; CDC versions must be resolved. Includes `ENABLED` and `PRIMARY_DEVICE`. |
| GPS | Equipment `COLLECTION_UID` -> `dbo.sitewatch_gps_raw.UID` | Verified. **Joining GPS directly to equipment UID is incorrect for the tested asset.** |
| Alternative minimal equipment mapping | `dbo.config_equipment` | Same GPN/equipment/collection mapping verified for `100111NZ`; also versioned. Prefer the richer table when fitment/device-selection flags are needed. |
| Assigned geographic area | GPS `ASSIGNED_GEO_ID` -> `dbo.sitewatch_config_geo_object.ID` | Verified. Site/geofence coordinates differ from the GPS fix and need a separate label. Do not presume `CURRENT_GEO_ID=0` is a usable site. |
| Recorded telemetry hours | Equipment `UID` -> `dbo.asset_status_current.UID` | Join verified, but example returns zero and an old observation despite recent ingestion. Definition/quality unresolved. |
| Connection mode | `dbo.unit_status_current` | Numeric `CONNECT_MODE` exists, but neither tested equipment nor collection UID matched. Other rows use a different-looking UID range. No verified identifier mapping or code dictionary; do not infer online/offline. |
| Service history | `dbo.v_service_data.ItemName` matches NOF individual item number | Strong candidate, verified for both assets with records through 2026. View combines current and historical service tables. |
| Service parts/actions | Fields in `dbo.v_service_data` | Verified part numbers/quantities and maintenance action codes. Descriptions can be null. |
| Historical service detail | `ABI.ServiceOrderAssignments`, `ABI.ServiceOrderHeader`, `ABI.ServiceOrderLines` | Rich older records; not the preferred primary feed because the combined service view supplied newer records for both examples. |
| Historical meter readings | Assignment `LastMeterHistoryID` -> `ABI.MeterHistory.MeterHistoryID` | Verified lookup. Distinct raw meter, engine-hour, and runtime fields plus bad/deleted entry flags. |
| Retrofits | `dbo.RetrofitCache.IndItem` matches NOF individual item number | Verified due and completed records, bulletin title, dates, and service-order references. |
| Maintenance components | `dbo.MaintenanceHistoryComponents.Plant_Number` | Schema inspected, not sample-validated. Nearly 9.7 million rows and only an ID index observed; defer when the service view already supplies parts. |
| Rental/agreement history | `dbo.asset_agreement_dim.gpn` | Verified against the Aggreko AI rental-history presentation for `100111NZ`: agreement number, customer number/name, valid-from, valid-to, and termination date. Overlapping validity periods can exist, so present these as source history rather than current assignment truth. |

## Verified GPS path and example

```text
NOF individual item / GPN: 100111NZ
    -> sitewatch_config_equipment.GPN
       equipment UID:   200000030320475
       collection UID:  210000030313035
    -> sitewatch_gps_raw.UID = collection UID
       latitude:        9.984044
       longitude:      -83.082230
       observation:     2026-09-20 23:30:01
       CDC timestamp:   2026-09-20 23:30:03
       ingestion:       2026-09-20 23:37:42.4515180
       validity:        1
       satellites:      7
       horizontal accuracy: 21 (unit not yet confirmed)
```

Source timestamps are `datetime2` without offsets. Confirm their timezone before displaying UTC/local time
or defining production age calculations. Do not use ingestion time as the GPS observation time.

The same fix has `ASSIGNED_GEO_ID=781`. The matching geographic object is named `Moin COSTA RICA`, at
`10.1017000, -83.5351000`; that is an assigned area, not the reported asset position. The precise map pin
in the earlier screenshot need not equal the later database fix.

`FC-039` exists in `dbo.ProductHierarchy` with `TelemetryStatus='0 - Not Fitted'`, warehouse `XG1`, and
current location `AU00028739`. No equipment mapping was returned for this GPN from either inspected
equipment table. `100111NZ` has `3 - Fitted - 3G/4G`, warehouse `ZP1`, and current location `CR00000001`.
These records are consistent with the different telemetry navigation states in the supplied screenshots.

### Coverage and freshness

At inspection, the raw GPS table contained 38,883 records. A provisional latest-version query, excluding
before-images and applying deletion after ranking, found:

| Population | Result |
| --- | ---: |
| Enabled primary equipment records with nonblank GPN | 26,015 |
| Distinct GPNs in that population | 25,179 |
| Equipment records with matching collection GPS | 24,844 |
| Matching records with `VALIDITY=1` and in-range coordinates | 24,261 |
| Equipment records with a fix within 24 hours, assuming source timestamps are UTC | 8,919 |
| Equipment records with a fix within 7 days, under the same assumption | 15,350 |
| Newest matched observation | 2026-09-21 05:29:13 |

These are database-discovery counts, **not percentages of the NOF fleet**. Some plant numbers have multiple
equipment records, and this population has not been intersected with NOF's authorised assets. It establishes
that location coverage is substantial but freshness cannot be assumed. Even the newest matched record was
several hours old when inspected; investigate source refresh cadence before promising live tracking.

`VALIDITY` has observed values 0 and 1. Coordinates exist even on rows with validity 0; both groups contain
zero-coordinate pairs. Obtain the source validity and accuracy definitions before selecting renderable fixes.
The table's versions do not establish a complete route-history feed.

### Change-data handling is essential

Equipment records include `INSERT`, `UPDATE`, `BULKLOAD`, `BEFOREIMAGE`, and `DELETE`. Sampled equipment
and hours tables contain repeated versions; many log positions are null. A simple join can return duplicates,
old mappings, or previously deleted devices.

Production requirements:

1. Confirm each source record's key and CDC ordering with the data owner. Inspection used UID partitions,
   source-change time, ingestion time, and log position; this is a discovery heuristic, not an approved merge rule.
2. Exclude before-images, establish the latest state, then honour delete tombstones. Filtering deletes before
   selecting the latest record can resurrect a removed device.
3. Apply enabled/primary selection after current state is resolved. Define a deterministic policy for multiple
   primary devices and unmapped/shared collection devices; do not arbitrarily choose the first row.
4. Select the GPS fix for the resolved collection device. Distinguish the newest state from the last valid fix;
   an older valid fix may be shown only with its real timestamp and an explicit label.
5. Preserve nulls, quality, and timestamp meaning. Do not turn missing readings into zero.

## Verified service history

`dbo.v_service_data` is a `UNION` of `dbo.service_data_history` and `dbo.service_data`, with explicit outer
projection. It removes exact duplicate rows; it does not produce one row per service order.

| Asset | Current table | History table | Combined view |
| --- | --- | --- | --- |
| `100111NZ` | 41 orders / 239 detail rows; 2023-10-09 to 2026-08-19 | 87 orders / 411 rows; 2011-12-13 to 2022-06-14 | 128 distinct service orders / 650 rows |
| `FC-039` | 4 orders / 6 rows; 2025-04-22 to 2026-01-23 | 31 orders / 196 rows; 2007-10-25 to 2022-02-26 | 35 distinct service orders / 202 rows |

All orders returned for these two assets had status `Closed`. These samples therefore prove completed
history, not open-work coverage. Gaps between date ranges must be checked with the owner; do not claim a
complete lifetime maintenance record from these counts alone.

Examples:

- `100111NZ`: order `0062036414`, created 19 August 2026, finished 21 August 2026, type `PMAgkOwnEquip`,
  action `A0013 - PM Completed`, recorded SMR 7,791. Two detail rows, one order.
- `FC-039`: order `0061563640`, created 23 January 2026, finished 27 January 2026, type `PMAgkOwnEquip`.
- An earlier `100111NZ` order includes actual part numbers and quantities (e.g. `BF7764`, quantity 2).
  Sampled part descriptions were null, so use an item-catalogue lookup only if needed and supported.

Suggested profile presentation: date, order number, work type, status, completion date, and an expandable
job detail showing symptoms, causes, actions, recorded meter readings, and parts. Group at a verified
asset/order/job grain; do not count parts rows as separate services. Decide how multiple jobs become an
order-level status. Keep technician identity optional and omit costs/customer details unless required.

The view has no division field. Authorise through NOF's asset record before querying `ItemName`, and verify
global asset/order identity uniqueness before relying on the join across regions.

### Meter readings and service due dates

Do not combine these numbers into an unlabeled "run hours" metric:

- SiteWatch `asset_status_current.HOURS` for `100111NZ` was zero, observed 25 August 2026, re-ingested
  21 September. Recent ingestion did not make it a fresh or necessarily usable meter observation.
- Service history records SMR 7,791 in August 2026; its unit and meter identity need confirmation.
- A linked historical ABI meter record in November 2021 has `MeterReadingValue=4360`, `EngineHours=97`,
  and `RunTimeHours=11751`. These are different measures, not interchangeable aliases.

Meter replacement/reset, cumulative-runtime calculations, units, and quality flags must be understood before
charting across sources or calculating the next service. `ABI.ServiceOrderAssignments` has service-interval
IDs, but no verified interval definition or next-service rule was found. Show recorded service facts first.

## Retrofits and other asset details

`RetrofitCache` provides a useful separate panel: bulletin/document, description, status, opened/completed
dates, and associated service order. `100111NZ` has several records marked `Due`, including one opened
4 June 2026, and completed records as well. Present the upstream status and source; validate refresh cadence
before using it to drive operational decisions. Do not treat a retrofit as a reservation or maintenance booking.

`ProductHierarchy` has additional master fields. `AllItemsCache`, `AllItemsCache_history`,
`v_AllItemsCache_history`, and `v_AssetSearch` expose further asset/reference metadata; the search view
filters the combined cache to plant-number records. Their presence is a future enrichment route, not proof
that every attribute/document shown on the website is available through the same interface.

## What is not yet supported by the evidence

- No verified actual fuel-level, load/kW, temperature, pressure, voltage, or RPM time-series source was found
  in the inspected table/column metadata. Configuration such as nominal voltage, fuel capacity, and alarm
  settings is not a measured reading.
- No verified online/offline code mapping, alarm feed, acknowledgement workflow, or full GPS route history.
- No proven next-service-due calculation, complete open-work list, or complete lifetime service coverage.
- No confirmed GPS accuracy units, validity-code semantics, source timezones, or agreed source refresh SLA.

## IPG SiteWatch integration follow-up

The Integration Orchestrator SiteWatch application was reviewed before extending NOF telemetry. It has a
scheduled `GetTelemetryValues` function, but this is a synchronisation process rather than a read API for
measurements or alarms. It reads the SiteWatch operational database, derives one of six fitment/inspection
states, and updates M3/FDP telemetry status through Service Bus. Its public asset API exposes the resulting
M3 status only.

The IPG query establishes the device path `config_equipment.GPN -> collection_uid -> config_collection.unit_uid`
and consults `unit_status_current`, equipment-specific current-reading tables, and `ALARMS_CURRENT`. NOF should
reuse those identity semantics. It should not call the IPG asset endpoint for rich telemetry because that
endpoint does not return readings, timestamps, or alarm history.

Read-only MDP validation confirmed that `config_collection`, `unit_status_current`, and
`asset_status_current` are replicated, while the alarm definitions/current alarms and equipment-specific
reading tables used by IPG are not present in the inspected MDP schema. For `100111NZ`, the verified chain is:

```text
equipment UID 200000030320475
  -> collection UID 210000030313035
  -> unit UID 357042062956967
  -> latest unit-status source change 2026-09-21 23:40:59
```

The unit-status replica contains a numeric connection-mode code but no documented dictionary and no source
`data_timestamp`. NOF therefore labels the CDC source-change time as **Device status updated** and explicitly
does not present it as online/offline or last contact. The asset-status hours value remains excluded because
the tested value conflicts with service meter history and its unit/reset semantics are unresolved.

The next source requirement is an owner-supported alarm/measurement API or replication of the required
SiteWatch current tables with definitions, units, observation timestamps, access rules, and retention. Until
then, fuel/load/temperature charts and alarm history remain outside the supported contract.

## Performance and integration requirements

`sitewatch_config_equipment` (~250k rows), `sitewatch_gps_raw` (~39k), `asset_status_current` (~539k), and
`RetrofitCache` (~287k) were heaps with no indexes reported in the inspected metadata. Latest-state queries
may scan data; do not execute full-fleet windowing for every profile request. Prefer an owner-supported
current-state view/materialisation or bounded cached lookup. Do not create indexes in this upstream database
without its owner's agreement. The discovery queries used bounded results and timeouts, not a load test.

The service tables are large (~10m current and ~19.6m historical rows), but both have covering indexes
beginning with `(ItemName, ServiceOrders_DateCreated)`. Filter by the authorised asset, date range, and page
size; keep order/job grouping and detail loading bounded. Confirm predicate pushdown and actual query plans
for the union view before production. `ABI.ServiceOrderLines` is ~50m rows; avoid it for initial browsing when
the prepared service view already provides the required fields.

The initial implementation uses one independently loaded route:

- `/api/assets/{id}/enrichment`: fitment, last reported GPS and quality, bounded service-order/job summaries,
  retrofit history, and up to 100 recent rental/agreement rows. `serviceLimit` is constrained to 1-50 and
  defaults to 20.

All use NOF authentication and asset division checks before MDP access or cache delivery. Use parameterised
SQL, cancellation, timeouts, bounded retries/cache, and explicit unavailable states. Browser code receives
neither database credentials nor arbitrary SQL/device lookup capability. Keep the existing booking/profile
endpoint independent so upstream failures cannot break planning. No NOF database schema change is inherently
needed for a read-only first release.

Retain the initial map-provider, attribution, accessibility, localisation, theme, and print requirements.
The first implementation uses an OpenStreetMap embed and link, so it adds no frontend map package or API key.
The provider choice remains subject to production support, privacy, and content-security review.

## Recommended delivery scope

1. Resolve application credentials, data-owner support, CDC/current-state rules, timezone, and query performance.
2. Add a last-reported-location panel with map, timestamp, fitment, and no-GPS/stale/error states.
3. Add service history with date filtering, distinct order/job summaries, details, and recorded meter values.
4. Add retrofit status/history if desired; keep richer telemetry and service-due calculations as separate work.

Indicative engineering effort after access/contracts are settled: location/map 4-7 working days; service
history an additional 3-5; retrofits an additional 1-2. These are planning estimates for one developer,
including focused checks, not commitments. Current-state materialisation or another telemetry feed would
need separate estimation. The principal data-discovery step has now been completed for GPS and service history.

Acceptance should include both supplied examples, a multi-device asset, invalid/stale/missing GPS, deletion
and duplicate CDC records, date/units checks, inaccessible assets, paged large service history, and source
failure. Verify current-source row selection with the data owner and run focused backend/frontend tests,
the frontend build, and visual/keyboard/print checks when implementation starts.
