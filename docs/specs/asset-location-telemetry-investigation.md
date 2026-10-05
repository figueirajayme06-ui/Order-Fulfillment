# Asset location and telemetry: feasibility investigation

Date: 21 September 2026. Status: research plus an initial implementation; runtime validation and deployment remain pending.

**Database discovery completed:** GPS, service history, retrofits, and rental/agreement history were identified. Read the
[verified data sources, joins, samples, and implementation requirements](../../build/docs/design/asset-location-service-data-investigation.md)
for the latest findings. They supersede the initial source-access assumptions below.

## Assessment

Integrating last reported location and a map into the current NOF asset profile is technically feasible.
Read-only database discovery has now verified a GPS mapping, service history through 2026, and retrofit
records. The next requirements are correct application credentials, supported current-state selection,
source timestamp/quality definitions, and performance validation. Rich telemetry, alarms, and live tracking
remain separate capabilities without a verified measurement feed in this investigation.

Evidence reviewed: current NOF source and documentation, local Aggreko-Forms source, the two supplied
screenshots, and official map-provider documentation. No live asset API, production data, network request,
or upstream credentials were inspected during the initial review. See the follow-up below for the supplied
URL, confirmed connection wiring, and resolved database-access investigation. The linked integration report
contains the subsequent live, read-only data findings, including the user's added service-history scope.

## Follow-up: existing MDP connection

The source website is `https://ai.aggreko.biz/asset-view`. The public web research tool could not access
the page. The user identified `MDPConnectionString` in `kv-ofdev` as the existing connection to
`tcp:sqlsfdplv.database.windows.net,1433`.

Source inspection confirms:

- `OF.Common/DependecyRegistration.cs` registers `MDPDbContext` with SQL Server using `MDPConnectionString`.
- `OF.Data.Warehouses/UseCases/GetWarehousesHandler.cs` reads `OrganisationalHierarchy`, excluding
  warehouse code `UNKNOWN`, then stages and merges the warehouse catalogue into NOF.
- `build/k8s/base/cronjob-warehouses.yaml` schedules this job at minute 20 of every hour. This is the
  repository schedule, not verification of the currently deployed runtime.
- `MDPDbContext` currently maps only `OrganisationalHierarchy` and `ProductHierarchy`. Neither mapped
  model includes coordinates or measured telemetry. Discovery subsequently confirmed additional unmapped
  objects for GPS, service history, and retrofits.
- `OF.WebApp/Program.cs` loads Key Vault configuration and invokes `RegisterCommonDependencies`.
  The web API therefore already has the code wiring to resolve this same MDP context; production
  credentials, object permissions, and network connectivity still require runtime verification.
- `FDPConnectionString` is a separate PostgreSQL connection and must not be confused with this MDP
  SQL Server connection.

An authorised read-only MDP query is the recommended integration path for the verified data. Proposed flow:
asset profile -> NOF observations endpoint -> authorised asset lookup -> parameterised MDP query, with
owner-approved current-state selection. Reuse connection configuration and backend infrastructure, not the warehouse
job's staging/truncate/merge workflow. Choose refresh/cache policy according to source observation cadence;
the hourly warehouse schedule is not a suitable assumption for current telemetry.

The Key Vault credential returned SQL error 18456. Correcting PowerShell handling and explicitly using UTF-8
did not resolve it. The user-supplied credential then connected successfully; a non-disclosing comparison
confirmed that its password differs from the parsed vault password. No vault or database changes were made.
Read-only metadata, GPS, service, meter, and retrofit queries subsequently succeeded. Establish the correct
supported read-only application credential before implementation; deployed job health was not tested.

The GPS collection-device join and service-history keys are now verified for the example assets. Remaining
proof concerns production CDC ordering, multi-device selection, source timezones/units, source update cadence,
fleet-wide identity coverage, and load/query-plan validation. Exact website-to-database lineage remains
unverified because its implementation was not inspected.

## Confirmed findings and limits

| Finding                                                                       | Evidence and implication                                                                                                                                                                                                                                                                               |
| ----------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| NOF has an existing asset profile                                             | `src/OF.Frontend/src/pages/assets/AssetProfilePage.tsx` renders planning summaries, bookings, asset/location details, and equipment details. Extend this page.                                                                                                                                         |
| An authenticated profile endpoint already exists                              | `src/OF.WebApp/Controllers/AssetsController.cs`, `GetAssetProfile`, serves `/api/assets/{id}/profile` and checks division access.                                                                                                                                                                      |
| Location text and some equipment fields already exist                         | `src/OF.WebApp/Features/Assets/AssetProfileScheduleBuilder.cs` exposes warehouse, warehouse location, facility, service centre, manufacturer, product group/category, run hours, and telemetry status.                                                                                                 |
| Existing telemetry status is not proof of live connectivity                   | `src/OF.Common/Infrastructure/IPG/Orders/Models/RAA/AssetSync/AssetData.cs` maps `TelemetryEnabled` to `TelemetryStatus`. CloudSuite `get-assets.sql` reads `CFI5` into the same field. Confirm its value dictionary before presenting it as connection health.                                        |
| Existing run hours are an imported value                                      | `src/OF.Common/Infrastructure/CloudSuite/SQL/get-assets.sql` reads `MVA0` as run hours. There is no measurement timestamp in the profile contract. Do not label this value live.                                                                                                                       |
| GPS coordinates are missing from the current asset model and profile contract | `src/OF.Data/Database/Asset.cs` and the profile response have no latitude/longitude or GPS observation timestamp. Warehouse location comes from `WHSL`, an operational location code, not coordinates.                                                                                                 |
| Asset join keys were verified for the samples                                 | NOF individual item number matches `ProductHierarchy.IndividualItemNumber`, equipment `GPN`, service `ItemName`, and retrofit `IndItem` as applicable. `100111NZ` GPS uses the equipment's `COLLECTION_UID`, not its equipment UID. See the integration report for missing-map and multi-device cases. |
| Local Aggreko-Forms appears to be a different application                     | Its UI is Vue 2 forms/submissions; searched source contains no equivalent equipment map or telemetry implementation. Its API queries `EnoviaViewer` for service centres and retrofit metadata. This is an investigative lead, not evidence that EnoviaViewer holds GPS or readings.                    |
| Screenshot behaviour varies by asset                                          | `100111NZ` shows a map and enabled-looking Telemetry navigation; `FC-039` does not show a map and has greyed Telemetry navigation. Coverage, permissions, and the reasons for these differences are unknown. Neither image establishes available telemetry metrics or their freshness.                 |
| Mapping needs configuration                                                   | The supplied map visibly shows CARTO's API-key watermark. This concerns basemap access and does not prove whether the asset coordinate feed is functioning or current.                                                                                                                                 |

## Recommended first release

User: a fleet planner checking where an asset was last reported before making a planning decision.

Add a compact location panel to the existing asset profile, near the planning context, containing:

- Last reported position, a small map, observation time and explicit age/staleness.
- Location source and type: GPS, reported site, or depot fallback.
- Existing warehouse/service-centre information, clearly distinguished from physical position.
- A refresh control, independently of the booking period.
- Optional latest supported measurements after feed verification, each with units and observation time.
- Service history as requested: paged order/job summaries, completion dates, actions, recorded meter readings,
  and expandable parts. Retrofit status is an additional verified enrichment option.

Keep booking availability and commitments authoritative for planning. GPS and telemetry must not automatically
change availability, reservations, or readiness. A warehouse/site coordinate fallback must be explicitly
labelled; it must never appear as a measured asset GPS position.

Defer historical routes, geofencing, alerts, alarm acknowledgement, remote control, and a fleet-wide map.
Do not reproduce the entire source equipment application inside NOF.

## Asset-profile UI refinement

- **User and job:** a fleet planner needs to understand availability and commitments first, then inspect the
  asset's last reported position or maintenance evidence when it affects a decision.
- **Existing pattern:** retain the asset profile's summary, schedule, semantic tables, native controls, shared
  tokens, and print hooks.
- **Information hierarchy:** keep identity and a compact planning snapshot visible; place the bookings timeline
  next; show location by default within one operational-data surface; collapse service history, rental history,
  retrofits, and lower-frequency asset fields behind native disclosure controls.
- **States:** preserve independent loading, empty, stale-refresh error, and retry states for MDP data. A failed
  enrichment request must not hide the schedule or core asset details.
- **Interaction:** disclosure summaries remain keyboard-operable and state their contents before expansion. The
  responsive layout stacks summary values and map content; print reveals disclosure content while hiding the map.
- **Acceptance:** warehouse, division, individual-item, location, and telemetry values do not appear in multiple
  adjacent panels; the schedule precedes supporting operational data; the initial page is materially shorter;
  focused tests, lint, formatting, build, desktop/mobile visual review, and print review pass.

## Proposed integration

```text
NOF asset profile
  -> OF.WebApp authenticated asset-observations endpoint
     -> NOF asset lookup and division authorisation
     -> existing MDP SQL connection with supported current-state queries
  -> approved map provider for basemap rendering
```

Use the verified MDP data sources with the owning team's support and agreed freshness semantics. Reuse data
rather than scrape or embed the source screen. A separate supported telemetry API remains an option for
rich measurements not verified in the database.

The initial implementation adds `GET /api/assets/{id}/enrichment` as a separately loaded route so a slow or
unavailable MDP source does not block the existing schedule/profile response. It resolves the upstream
identifier from the authorised NOF asset and does not accept arbitrary device IDs from the browser. The
response combines the latest location, bounded service-order/job summaries, and retrofit history.

Use a typed backend query service, request cancellation, bounded timeouts, bounded retries, and a short
configurable cache. Authorise before serving cached data, and scope the cache to the upstream access model.
Keep upstream service credentials on the server. Reuse existing EasyAuth and division policy, but confirm
whether the telemetry service imposes additional customer/region permissions. Application credentials
must not accidentally broaden user access.

No database schema change is intrinsically required for an on-demand latest-reading release. A persistent
identifier mapping, retained history, or ingestion service would need separate design if discovery proves
it necessary. Any shared database change belongs in `OF.Data.Design`.

### Minimum source contract to confirm

| Area                       | Required information                                                                                                                                                                                          |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Identity                   | Stable plant/individual-item ID; device mapping; handling of replacement devices, aliases, and duplicate IDs across regions. Do not join on a product/item code alone.                                        |
| Location                   | Nullable latitude/longitude, coordinate reference system (normally WGS84), source/type, observation time with timezone, optional accuracy.                                                                    |
| Timing                     | Distinguish measured/observed time from ingestion time and NOF fetch time. Establish typical reporting cadence and stale threshold.                                                                           |
| Coverage                   | Telemetry capable/enabled, no reading, never reported, stale, and unavailable states; expected asset classes/regions.                                                                                         |
| Measurements               | Stable metric identifier, value, unit, timestamp, quality, and supported asset class. Candidate metrics include running state, fuel, load, run hours, or temperatures; none are confirmed by the screenshots. |
| Access                     | API owner, supported endpoints/version, dev/test environments, service identity or delegated-token requirements, network access, and permission scope.                                                        |
| Operations                 | Rate limits, expected concurrency, availability/support ownership, cache rules, and handling of upstream failures.                                                                                            |
| History, if later required | Retention, maximum time range, pagination, aggregation, timezone, and alarm semantics.                                                                                                                        |

Validate coordinates as finite values in valid ranges; treat zero-valued coordinates according to source
quality flags, not as universally missing. Preserve missing readings as null rather than inventing zeros.

Define explicit outcomes for not equipped, no data, stale data, upstream failure, and forbidden access.
Retain NOF's unauthenticated and inaccessible-asset behaviour. An upstream failure must not be returned as
an empty successful measurement set; cached last-known values must disclose their age and refresh failure.

## Map requirements

Select the provider based on Aggreko's existing account and support arrangements. The map library and the
basemap/data provider are separate choices; no map dependency currently appears in NOF's `package.json`.

- CARTO reuse is an option if the organisation already supports it. Its official guidance requires a key
  and visible attribution. Configure environment-specific access and suitable key restrictions; validate
  the commercial usage arrangement before choosing it. See [CARTO key guidance](https://carto.com/basemaps/apikey/).
- Azure Maps is another candidate in NOF's Azure environment. Microsoft documents Web SDK access through
  a backend token service and managed identity. This requires account/RBAC configuration, not just adding
  a widget. See [Microsoft's SPA authentication guidance](https://learn.microsoft.com/en-us/azure/azure-maps/how-to-secure-spa-app).

Confirm current pricing, usage volume, attribution, allowed browser origins, network/content-security
rules, and whether sending the relevant map viewport to the provider is permitted. No account has been
created and no provider or dependency has been selected in this investigation.

Load the map only for an opened profile with a usable position. Keep a textual position/time alternative,
keyboard-operable controls, readable light/dark themes, mobile stacking, and a useful print summary.
Map failure must leave the location text and other profile content usable. Localise new visible text.

## Delivery plan and indicative effort

These are engineering estimates for one developer, not commitments. They assume supported source queries,
working application access, agreed identity selection, an approved map provider, and no new ingestion pipeline. They
exclude external access/procurement waiting time and could change substantially after source inspection.

| Stage                                  | Output                                                                                                                                            | Indicative effort                                           |
| -------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------- |
| Discovery/proof                        | GPS and service-data sources/sample joins verified; remaining source semantics and application-access checks are listed in the integration report | Initial data proof completed                                |
| Location MVP                           | Backend adapter and access checks, independent location endpoint, map panel, failure/freshness states, focused tests and visual verification      | 4-7 working days                                            |
| Service history                        | Paged order/job summaries, recorded meter values, expandable actions/parts, tests                                                                 | Additional 3-5 working days                                 |
| Retrofits                              | Bulletin status/history panel and tests                                                                                                           | Additional 1-2 working days                                 |
| Latest measurements                    | Agreed small metric set, equipment-specific support, units, timestamps, refresh and tests                                                         | Additional 3-6 working days if readings are already exposed |
| Telemetry history/alarms/live tracking | Separate contract, UI, scale/retention analysis and estimate                                                                                      | Not estimated until source capabilities are known           |

Suggested implementation sequence: source contract and mapping proof; backend integration; profile panel
and map; optional metrics; acceptance verification. Review the proposed API and dependency choices before
implementation in accordance with `src/OF.Frontend/AGENTS.md`.

## Acceptance and verification requirements

- A permitted asset with a verified reading displays the correct position, source, and observation time.
- A source reading agrees with NOF for several representative assets, including both screenshot examples
  if available and a non-reporting asset. Sample more than one region and equipment class.
- Depot/site fallbacks and stale readings cannot be mistaken for current GPS.
- Users cannot retrieve observations for assets outside their authorised scope, including through cache.
- Measurement units, timestamps, missing values, and unsupported metrics are handled explicitly.
- A timeout, rate limit, or failed map tile request does not prevent access to bookings and profile details.
- Switching assets does not leave the previous asset's pin/readings visible; changing schedule dates does
  not imply that current readings represent a historical position.
- Validate backend mapping/access/failure cases and frontend loading/empty/stale/error states with focused
  tests. Run `npm run build`; perform desktop, mobile, keyboard, theme, and print checks. Automated checks
  cover the initial API and profile component; live MDP and deployed-browser validation remain pending the
  corrected application credential and target environment.

## Outstanding decisions

1. Source website owning team/repository: URL is now supplied; the local Forms project does not establish its API.
2. Correct application credentials and owner-supported current-state queries; direct investigation now works,
   while the parsed Key Vault password differs from the working credential.
3. Meaning of the website's Current Location code and map pin: GPS, installation site, or another location.
4. Identifier mapping, telemetry coverage, reporting frequency, and freshness thresholds.
5. Priority measurements and whether latest readings alone satisfy the first release.
6. Existing approved map-provider account and production configuration.

The location and service-history data proof is complete. Next, settle source semantics and read-only
application access, then review the bounded API/UI implementation described in the linked integration report.
