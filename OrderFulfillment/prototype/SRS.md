# Software Requirements Specification
## OrderFulfillment System

**Version:** 1.0  
**Methodology:** Traditional  
**Status:** Draft  
**Date:** 2026  

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Overall Description](#2-overall-description)
3. [Business Requirements and Rules](#3-business-requirements-and-rules)
4. [Use Cases](#4-use-cases)
5. [Functional Requirements](#5-functional-requirements)
6. [Non-Functional Requirements](#6-non-functional-requirements)
7. [Data Requirements](#7-data-requirements)
8. [External System Interfaces](#8-external-system-interfaces)
9. [Assumptions, Dependencies & Constraints](#9-assumptions-dependencies--constraints)
10. [Risk Analysis](#10-risk-analysis)
11. [Glossary](#11-glossary)

---

## 1. Executive Summary

The **OrderFulfillment** system is a .NET 10, Azure-hosted, cloud-native platform that serves as the central orchestration layer for managing the end-to-end lifecycle of rental equipment orders.

### Business Problem

The rental equipment business operates across multiple disconnected enterprise systems: Salesforce (opportunity and quote origination), IPG (order management and pricing), and CloudSuite/Infor (agreement lifecycle and activation). Without a unified orchestration layer, order data is fragmented, manual handoffs introduce errors and delays, and there is no single source of truth for fulfilment status.

### Solution Overview

OrderFulfillment bridges these systems through a combination of message-driven (Azure Service Bus) and REST-based integrations.

| Capability | Description |
|---|---|
| Automated Opportunity Import | Pulls high-probability opportunities from Salesforce with intelligent deduplication (30-min cache + DB check) |
| Asset Reservation Management | Links physical equipment to order lines with real-time fulfilment status tracking (Unfulfilled → Partially → Fully → Finished) |
| Agreement Activation | Validates, submits to IPG, and coordinates asynchronous acknowledgement from CloudSuite (TODO → Requested → Activated/Failed) |
| Change Order Processing | Handles post-activation modifications via BOD messages with version-ordered tracking (ChangeSequence) |

### Key Stakeholders

| Stakeholder | Role |
|---|---|
| Fulfilment Staff | Daily users — reserve assets, manage orders within their division |
| Operations Managers | Reporting, oversight, cross-division visibility |
| IT / Integration Teams | Manage external system connections, monitor Service Bus health |

### Security & Compliance

- All secrets managed exclusively via Azure Key Vault
- External authentication via OAuth2 bearer tokens
- Internal services use Azure managed identity
- Division-based data isolation at application layer
- Full audit trail (AuditLog table + LastUpdatedBy/Date on all entities)

### Strategic Roadmap

| Initiative | Status |
|---|---|
| AKS Migration | Complete |
| .NET 10 Upgrade | Complete |

---

## 2. Overall Description

### 2.1 System Purpose

OrderFulfillment manages the end-to-end lifecycle of rental equipment orders — from opportunity capture through reservation, activation, and post-activation change management.

### 2.2 User Roles

| Role | Description |
|---|---|
| Standard User | Division-scoped; can view and manage orders/reservations within their division |
| Admin | `IsAdmin = true`; elevated access across divisions |
| Super Admin | `IsSuperAdmin = true`; full system access |
| CloudSuite/Infor | Pushes BOD messages via Azure Service Bus; receives activation requests |
| IPG | Provides quote data and receives activated order lines |
| Salesforce | Source of opportunities and user language preferences |

### 2.3 Technology Stack

- **Runtime**: .NET 10
- **Cloud Platform**: Azure (Service Bus, Blob Storage, Table Storage, Key Vault, Application Insights)
- **Architecture**: Microservices with separated read contexts (CPQ, MDP, FDP)
- **Integration**: Message-driven (Azure Service Bus) + REST APIs
- **Sync Pattern**: Watermark-based incremental sync (Azure Table Storage)
- **Caching**: In-memory cache with 30-minute TTL for deduplication

### 2.4 Service Bus Queues

| Queue | Direction | Purpose |
|---|---|---|
| `UpdateByAgreement` | Inbound | Agreement updates from CloudSuite/Infor M3 |
| `UpsertQuote` | Inbound | Quote/opportunity data from Salesforce |
| `ActivateAgreement` | Outbound | Activation requests to CloudSuite |
| `ActivateHeader` | Outbound | Header-level activation messages |
| `ChangeNotify` | Inbound | Change notification BOD messages |
| `ChangeApproval` | Inbound | Change approval BOD messages |
| `ChangeComplete` | Inbound | Change completion BOD messages |
| `AgreementSync` | Inbound | Agreement synchronisation from Infor |

### 2.5 Service Bus Topics

| Topic | Direction | Purpose |
|---|---|---|
| `order-activation-ack` | Inbound | Activation acknowledgement from Infor M3 |
| `order-header-sync` | Inbound | Header-level sync from Infor M3 |
| `order-line-ack` | Inbound | Line-level activation acknowledgement |
| `order-line-sync` | Inbound | Line-level sync from Infor M3 |

---

## 3. Business Requirements and Rules

### 3.1 Quoting Rules

| ID | Rule | Type |
|---|---|---|
| BR-QT-001 | Opportunities shall only be imported when probability ≥ configurable threshold (default: 90%) | Threshold |
| BR-QT-002 | Deduplication shall use a two-layer approach: 30-minute in-memory cache check THEN database existence check | Constraint |
| BR-QT-003 | Opportunities marked "Closed Lost" or "Not Accepted" shall trigger soft-deletion of their mapped internal orders | Lifecycle |
| BR-QT-004 | Hire dates on imported quote lines shall be corrected if invalid or in the past | Validation |

### 3.2 Fulfilment Status State Machine

| ID | Rule | Type |
|---|---|---|
| BR-FS-001 | FulfilmentStatus on Line and Header shall be recalculated after every reservation change | Computation |
| BR-FS-002 | Valid state transitions: Unfulfilled → PartiallyFulfilled → FullyFulfilled → Finished | State Machine |

### 3.3 Activation Rules

| ID | Rule | Type |
|---|---|---|
| BR-ACT-001 | A line shall only be activated if `RequiresFulfilment = true` AND it is not already activated | Precondition |
| BR-ACT-002 | Agreement numbers must be unique system-wide; duplicates shall be rejected | Uniqueness |
| BR-ACT-003 | Orphaned quote lines shall be abandoned (soft-deleted) during activation | Lifecycle |
| BR-ACT-004 | ActivationStatus transitions: TODO (0) → Requested (2) → Activated (3) OR Failed (1) | State Machine |

### 3.4 Reservation Rules

| ID | Rule | Type |
|---|---|---|
| BR-RES-001 | A Reservation links exactly one Asset to exactly one Line, with quantity and warehouse | Structure |
| BR-RES-002 | The actual asset fulfilled may differ from the initially planned asset (`ActualAssetId`) | Flexibility |
| BR-RES-003 | Reservations support flags: IsDepotFulfilled, IsRehire, IsConfirmed | Classification |

### 3.5 Change Order Rules

| ID | Rule | Type |
|---|---|---|
| BR-CO-001 | Post-activation changes arrive exclusively via BOD messages (ChangeApproval, ChangeComplete, ChangeNotify) | Integration |
| BR-CO-002 | ChangeSequence (bigint) on Headers and Lines shall monotonically increase with each change | Ordering |

### 3.6 Data Integrity Rules

| ID | Rule | Type |
|---|---|---|
| BR-DI-001 | Records shall never be physically deleted; all deletions use `IsDeleted` flag | Soft Delete |
| BR-DI-002 | Division scoping governs which users can view which orders and assets | Access Control |
| BR-DI-003 | Unique constraints enforced on: AgreementNumber, OrderNumber, QuoteNumber | Uniqueness |

### 3.7 Ringfence Rules

| ID | Rule | Type |
|---|---|---|
| BR-RF-001 | A Ringfence (FromDate, ToDate, Divisions, Warehouse) blocks matching assets from fulfilment for the specified period | Constraint |

---

## 4. Use Cases

### UC-01: Import Opportunities from Salesforce

| Field | Description |
|---|---|
| **Actor** | System (scheduled/triggered) |
| **Precondition** | Salesforce API accessible; probability threshold configured |
| **Main Flow** | 1. Query Salesforce via SOQL for opportunities with probability ≥ threshold<br>2. Check 30-min memory cache for duplicates<br>3. Check database for existing quotes<br>4. For new opportunities: fetch quote lines from IPG, correct hire dates, persist as Header + Lines<br>5. For "Closed Lost"/"Not Accepted": soft-delete mapped orders |
| **Postcondition** | New Headers/Lines created OR existing orders soft-deleted |
| **Exception** | Salesforce unavailable → log error, retry on next cycle |

### UC-02: Reserve Asset Against Order Line

| Field | Description |
|---|---|
| **Actor** | Standard User (fulfilment staff) |
| **Precondition** | Line exists; Line is not fully fulfilled |
| **Main Flow** | 1. Staff selects an order line<br>2. Staff selects an asset (or actual asset differs from planned)<br>3. System creates Reservation with quantity, warehouse, notes<br>4. System recalculates FulfilmentStatus on Line and Header |
| **Postcondition** | Reservation created; status updated |
| **Exception** | Asset unavailable (ring-fenced) → reject with explanation |

### UC-03: Activate Agreement

| Field | Description |
|---|---|
| **Actor** | Standard User / Admin |
| **Precondition** | All lines sufficiently fulfilled; agreement number unique |
| **Main Flow** | 1. Staff triggers activation via API<br>2. System validates all reservations (throw ActivationException on failure)<br>3. System abandons orphaned quote lines<br>4. System sends remaining lines to IPG Order Management API<br>5. System places message on ActivateHeaderQueueClient<br>6. ActivationStatus → Requested |
| **Postcondition** | Activation request dispatched; awaiting CloudSuite acknowledgement |
| **Exception** | Validation failure → ActivationException; duplicate agreement → rejected |

### UC-04: Process Activation Acknowledgement

| Field | Description |
|---|---|
| **Actor** | External System (CloudSuite/Infor) |
| **Trigger** | BOD acknowledgement message received via Service Bus |
| **Main Flow** | 1. System receives acknowledgement BOD<br>2. System advances ActivationStatus: Requested → Activated (success) or Failed |
| **Postcondition** | Header reflects final activation state |
| **Exception** | Malformed BOD → log to ProcessingErrors table |

### UC-05: Process Change Order

| Field | Description |
|---|---|
| **Actor** | External System (CloudSuite/Infor) |
| **Trigger** | BOD message: ChangeApproval, ChangeComplete, or ChangeNotify |
| **Main Flow** | 1. System receives change BOD<br>2. System persists data across ChangeOrders/Headers/Lines/Addresses/Contacts/Comments<br>3. System increments ChangeSequence on affected Header and Lines |
| **Postcondition** | Change order recorded; version tracking updated |
| **Exception** | Out-of-order BOD → log warning, process based on ChangeSequence |

### UC-06: Manage Ringfence

| Field | Description |
|---|---|
| **Actor** | Admin / Super Admin |
| **Main Flow** | 1. Admin defines FromDate, ToDate, Divisions, Warehouse<br>2. System creates Ringfence record<br>3. Matching assets are blocked from fulfilment during the period |
| **Postcondition** | Assets excluded from fulfilment queries for specified period |

### UC-07: Fulfil Serialized Equipment

| Field | Description |
|---|---|
| **Actor** | Standard User (fulfilment staff) |
| **Main Flow** | 1. System executes FulfilSerialized stored procedure<br>2. Queries across assets, CPQ catalogue, reservations, and ring fences<br>3. Returns available serialized assets matching the order line criteria |
| **Postcondition** | List of available assets presented to staff |

### UC-08: Fulfil Non-Serialized Equipment

| Field | Description |
|---|---|
| **Actor** | Standard User (fulfilment staff) |
| **Main Flow** | 1. System executes FulfilNonSerialized stored procedure<br>2. Queries bulk/consumable inventory matching order line criteria<br>3. Returns available quantities by warehouse |
| **Postcondition** | Available quantities presented to staff |

### UC-09: View/Manage Orders by Division

| Field | Description |
|---|---|
| **Actor** | Standard User / Admin / Super Admin |
| **Main Flow** | 1. System filters orders by user's division (Standard) or all divisions (Admin/SuperAdmin)<br>2. User browses/searches orders<br>3. User drills into specific Header → Lines → Reservations |
| **Postcondition** | Division-scoped view of orders displayed |

---

## 5. Functional Requirements

### 5.1 Quoting

| ID | Requirement | Priority | Linked Use Case |
|---|---|---|---|
| FR-QT-001 | System shall query Salesforce via SOQL for opportunities with probability ≥ configurable threshold (default 90%) | High | UC-01 |
| FR-QT-002 | System shall deduplicate incoming quotes using a 30-minute in-memory cache AND a database existence check | High | UC-01 |
| FR-QT-003 | System shall soft-delete internal orders mapped to "Closed Lost" or "Not Accepted" opportunities | High | UC-01 |
| FR-QT-004 | System shall fetch quote lines from IPG Order Management API and persist them as Header + Line records | High | UC-01 |
| FR-QT-005 | System shall correct hire dates on imported quote lines if the original dates are invalid or in the past | Medium | UC-01 |

### 5.2 Reservation

| ID | Requirement | Priority | Linked Use Case |
|---|---|---|---|
| FR-RES-001 | System shall allow creation of a Reservation linking an Asset to a Line with quantity, warehouse, and optional notes | High | UC-02 |
| FR-RES-002 | System shall recalculate FulfilmentStatus on both Line and Header after each reservation change | High | UC-02 |
| FR-RES-003 | System shall support IsDepotFulfilled, IsRehire, and IsConfirmed flags on reservations | Medium | UC-02 |
| FR-RES-004 | System shall allow ActualAssetId and ActualItemNumber to differ from the initially planned values | Medium | UC-02 |

### 5.3 Activation

| ID | Requirement | Priority | Linked Use Case |
|---|---|---|---|
| FR-ACT-001 | System shall validate all reservations for correctness before activation; throw ActivationException on failure | High | UC-03 |
| FR-ACT-002 | System shall abandon (soft-delete) orphaned quote lines during the activation process | High | UC-03 |
| FR-ACT-003 | System shall send remaining activated lines to IPG Order Management API | High | UC-03 |
| FR-ACT-004 | System shall place a message on the `ActivateHeaderQueueClient` via Azure Service Bus | High | UC-03 |
| FR-ACT-005 | System shall advance `ActivationStatus` from TODO (0) → Requested (2) upon sending the activation request | High | UC-03 |
| FR-ACT-006 | System shall advance `ActivationStatus` from Requested (2) → Activated (3) or Failed (1) upon receiving acknowledgement BOD | High | UC-04 |
| FR-ACT-007 | System shall only activate lines where `RequiresFulfilment = true` AND `ActivationStatus ≠ Activated` | High | UC-03 |
| FR-ACT-008 | System shall reject activation if the Agreement Number is not unique | High | UC-03 |

### 5.4 Change Orders

| ID | Requirement | Priority | Linked Use Case |
|---|---|---|---|
| FR-CO-001 | System shall process inbound BOD messages of types: ChangeApproval, ChangeComplete, ChangeNotify | High | UC-05 |
| FR-CO-002 | System shall persist change data across: ChangeOrders, ChangeOrderHeaders, ChangeOrderLines, ChangeOrderAddresses, ChangeOrderContacts, ChangeOrderComments | High | UC-05 |
| FR-CO-003 | System shall maintain a ChangeSequence (bigint) on both Headers and Lines for version ordering | High | UC-05 |

### 5.5 Asset, CPQ & User Management

| ID | Requirement | Priority | Linked Use Case |
|---|---|---|---|
| FR-AST-001 | System shall track asset status: Available, RemovedStock, Scrap, Sold | Medium | UC-07, UC-08 |
| FR-AST-002 | System shall maintain EstimatedReadyDate and TelemetryStatus for each asset | Low | UC-07 |
| FR-CPQ-001 | System shall maintain a product catalogue (CPQ Generic) with GenericCode, Rating, UOM, substitutions, attributes | Medium | UC-07, UC-08 |
| FR-CPQ-002 | System shall map specific CPQ Items to their parent Generic with attribute values | Medium | UC-07 |
| FR-RF-001 | System shall support Ringfence records that block assets from fulfilment for a specified period | Medium | UC-06 |
| FR-USR-001 | System shall enforce division-scoped data access for Standard Users | High | UC-09 |
| FR-USR-002 | System shall provide elevated cross-division access for IsAdmin users and full access for IsSuperAdmin users | High | UC-09 |
| FR-USR-003 | System shall resolve user language preference via Salesforce user language service | Low | — |
| FR-USR-004 | System shall localise attribute names per user language via the AttributeLanguage table | Low | — |

---

## 6. Non-Functional Requirements

### 6.1 Performance

| ID | Requirement | Priority |
|---|---|---|
| NFR-PERF-001 | Fulfilment stored procedures (FulfilSerialized, FulfilNonSerialized) shall respond within interactive timeframes (< 5 seconds) when querying across assets, CPQ catalogue, reservations, and ring fences simultaneously | High |
| NFR-PERF-002 | Database schema shall maintain composite covering indexes on all high-traffic join paths (Assets, Lines, Reservations) | High |
| NFR-PERF-003 | Opportunity import deduplication shall use a 30-minute TTL in-memory cache to balance freshness vs. performance | Medium |
| NFR-PERF-004 | Data microservices shall use a watermark pattern (Azure Table Storage) for incremental syncs rather than full reloads. Schedules: Quotes every 20/50 mins, Assets every 30 mins (10/40 past), Warehouses hourly (20 past), NonSerialised daily at 12 UTC | High |
| NFR-PERF-005 | System shall maintain separate read-optimised database contexts (CPQ, MDP, FDP) for catalogue queries vs. transactional data | Medium |

### 6.2 Security

| ID | Requirement | Priority |
|---|---|---|
| NFR-SEC-001 | All connection strings and API keys shall be stored exclusively in Azure Key Vault; no secrets in code or config files | Critical |
| NFR-SEC-002 | CloudSuite integration shall authenticate using OAuth2 bearer tokens | High |
| NFR-SEC-003 | Internal Azure services shall use managed identity patterns (no shared keys) | High |
| NFR-SEC-004 | Division-based data scoping shall be enforced at the application layer for all Standard User queries | High |
| NFR-SEC-005 | Elevated access shall be controlled via IsAdmin / IsSuperAdmin flags with appropriate authorization checks | High |

### 6.3 Reliability & Availability

| ID | Requirement | Priority |
|---|---|---|
| NFR-REL-001 | Failed Service Bus message processing shall be captured in the ProcessingErrors table with full error context | High |
| NFR-REL-002 | System shall implement retry and dead-letter queue patterns for all BOD message processing | High |

### 6.4 Auditability

| ID | Requirement | Priority |
|---|---|---|
| NFR-AUD-001 | AuditLog table (with AuditLog_Staging) shall capture all data changes across mutable entities | High |
| NFR-AUD-002 | All mutable entities shall track LastUpdatedBy and LastUpdatedDate | High |

### 6.5 Observability

| ID | Requirement | Priority |
|---|---|---|
| NFR-OBS-001 | System shall integrate with Application Insights for telemetry, error tracking, and distributed tracing | Medium |

### 6.6 Data Integrity

| ID | Requirement | Priority |
|---|---|---|
| NFR-DI-001 | System shall use soft deletes only (IsDeleted flag); physical record removal is prohibited | High |
| NFR-DI-002 | System shall enforce unique constraints on AgreementNumber, OrderNumber, and QuoteNumber | High |

---

## 7. Data Requirements

### 7.1 Core Entity Schema

#### Header

| Field | Type | Constraints |
|---|---|---|
| Id | Int | PK, auto-increment |
| QuotePublicId | String | Nullable |
| AgreementNumber | String | Nullable |
| OrderNumber | String | Nullable |
| QuoteNumber | String | Nullable |
| CustomerName | String | Nullable |
| CustomerNumber | String | Nullable |
| Division | String | Not null |
| Facility | String | Not null |
| FulfilmentStatus | Int | Maps to OrderAssignmentStatus enum (1=Unfulfilled, 2=PartiallyFulfilled, 3=FullyFulfilled, 4=Finished) |
| ActivationStatus | Int | Maps to ActivationStatus enum (0=TODO, 1=Failed, 2=Requested, 3=Activated) |
| OnHireDate | DateTime | Nullable |
| OffHireDate | DateTime | Nullable |
| ChangeSequence | Long (bigint) | Monotonically increasing |
| IsDeleted | Boolean | Default: false |
| LastUpdatedBy | String | Audit field |
| LastUpdatedDate | DateTime | Audit field |
| OrderSource | String | Not null |
| OpportunityNumber | String | Nullable |
| OpportunityName | String | Nullable |
| OpportunityStage | String | Nullable |
| Probability | Double | Nullable |
| RentalDepot | String | Nullable |
| IsSkeleton | Boolean | Nullable |
| ActivationErrors | String | Nullable |
| ActivationInstanceId | String | Nullable |

#### Line

| Field | Type | Constraints |
|---|---|---|
| Id | Int | PK, auto-increment |
| HeaderId | Int | FK to Header (nullable) |
| ItemNumber | String | Nullable |
| GenericItemNumber | String | Nullable |
| Quantity | Float | |
| DeliveryDate | DateTime | Nullable |
| ValidFromDate | DateTime | Not null |
| ValidToDate | DateTime | Not null |
| TerminationDate | DateTime | Nullable |
| Attributes | String | Flexible attribute storage (nullable) |
| LocalizedAttributes | String | Nullable |
| FulfilmentStatus | Int | Same enum as Header (OrderAssignmentStatus) |
| ActivationStatus | Int | Same enum as Header |
| RequiresFulfilment | Boolean | Determines activation eligibility |
| IsDeleted | Boolean | Default: false |
| ChangeSequence | Long (bigint) | Monotonically increasing |
| Warehouse | String | Not null |
| Division | String | Not null |
| Facility | String | Not null |
| OrderSource | String | Not null |
| OrderLineNumber | String | Nullable |
| AgreementLineNumber | String | Nullable |
| AgreementLineType | String | Nullable |
| QuantityFulfilled | Double | Tracks how much has been reserved |
| NumberOfShifts | String | Nullable |
| RateType | String | Nullable |
| CollectionDate | DateTime | Nullable |
| LastUpdatedBy | String | Audit field |
| LastUpdatedDate | DateTime | Audit field |

#### Reservation

| Field | Type | Constraints |
|---|---|---|
| Id | Int | PK, auto-increment |
| AssetId | String | Not null |
| LineId | Int | FK to Line |
| ItemNumber | String | Not null |
| Quantity | Int | |
| EffectiveQuantity | Double | Calculated effective quantity |
| Warehouse | String | Not null |
| IsConfirmed | Boolean | Default: false |
| IsDepotFulfilled | Boolean | Default: false |
| IsRehire | Boolean | Default: false |
| ActualAssetId | String | Nullable (may differ from planned) |
| ActualItemNumber | String | Nullable |
| ActualQuantity | Double | Nullable |
| Notes | String | Nullable |
| LastUpdatedBy | String | Audit field |
| LastUpdatedDate | DateTime | Audit field |

#### Asset

| Field | Type | Constraints |
|---|---|---|
| Id | String | PK |
| IndividualItemNumber | String | Not null |
| ItemNumber | String | Nullable |
| Status | String | Nullable (e.g. Available, RemovedStock, Scrap, Sold) |
| StatusCode | String | Nullable |
| Warehouse | String | Nullable |
| Division | String | Nullable |
| Facility | String | Nullable |
| EstimatedReadyDate | DateTime | Nullable |
| TelemetryStatus | String | Nullable |
| IonLastModified | DateTime | Nullable — tracks last sync from ION Data Lake |
| AgreementNumber | String | Nullable — current agreement if on hire |
| CustomerName | String | Nullable |
| CustomerNumber | String | Nullable |
| DeliveryDate | DateTime | Nullable |
| RunHours | Double | Nullable |

### 7.2 Data Integrity Rules

| Rule | Description |
|---|---|
| Unique Keys | AgreementNumber, OrderNumber, QuoteNumber must be globally unique |
| Foreign Keys | Header → Lines → Reservations → Assets (cascading relationships) |
| Soft Delete | All deletions via IsDeleted flag; no physical record removal |
| Audit Trail | LastUpdatedBy + LastUpdatedDate on all mutable entities |
| Version Control | ChangeSequence (bigint) on Headers and Lines for ordering |

---

## 8. External System Interfaces

### 8.1 External Interfaces

| ID | System | Direction | Protocol | Authentication | Data |
|---|---|---|---|---|---|
| IF-001 | Infor M3 / CloudSuite (via ION) | Inbound + Outbound | Azure Service Bus (BOD messages) | OAuth2 Bearer Token | Agreement BODs, activation acknowledgements, asset sync, CPQ catalogue, warehouse data |
| IF-002 | IPG Order Management API | Outbound + Inbound | REST (HTTPS) | API Key / OAuth2 | Send activated lines; retrieve quote data |
| IF-003 | IPG Order Integration API | Outbound | REST (HTTPS) | API Key / OAuth2 | Agreement line updates |
| IF-004 | IPG Pricing API | Outbound | REST (HTTPS) | API Key / OAuth2 | Pricing data for orders |
| IF-005 | Salesforce | Inbound (query) | SOQL via Salesforce API | OAuth2 | Opportunity/quote source, user language |
| IF-006 | ION Data Lake | Inbound | Azure Service Bus (via CloudSuite) | OAuth2 | Serialized asset data, product items |

### 8.2 Azure Infrastructure Interfaces

| ID | Service | Direction | Purpose |
|---|---|---|---|
| IF-007 | Azure Service Bus | Bidirectional | Queues: UpdateByAgreement, UpsertQuote, ActivateAgreement, ActivateHeader, ChangeNotify, ChangeApproval, ChangeComplete, AgreementSync. Topics: order-activation-ack, order-header-sync, order-line-ack, order-line-sync |
| IF-008 | Azure Blob Storage | Read | Product hierarchy data from hierarchy container |
| IF-009 | Azure Table Storage | Read/Write | Watermark table for incremental data sync tracking |
| IF-010 | Azure Key Vault | Read | All connection strings, API keys, and secrets |
| IF-011 | Application Insights | Write | Telemetry, error tracking, distributed tracing |

### 8.3 Service Bus Queue Contracts

| Queue Name | Direction | Trigger | Payload |
|---|---|---|---|
| UpdateByAgreement | Inbound | CloudSuite/Infor M3 BOD | Agreement update data (change orders, status changes) |
| UpsertQuote | Inbound | Salesforce import | Quote/opportunity data for new or updated orders |
| ActivateAgreement | Outbound | Staff triggers activation | Activation request with validated line data |
| ActivateHeader | Outbound | Activation workflow | Header-level activation message for Infor M3 processing |
| ChangeNotify | Inbound | CloudSuite/Infor M3 BOD | Change notification data |
| ChangeApproval | Inbound | CloudSuite/Infor M3 BOD | Change approval data |
| ChangeComplete | Inbound | CloudSuite/Infor M3 BOD | Change completion data |
| AgreementSync | Inbound | CloudSuite/Infor M3 | Agreement synchronisation data |

---

## 9. Assumptions, Dependencies & Constraints

### 9.1 Assumptions

| ID | Assumption | Impact if Invalid |
|---|---|---|
| A-001 | Salesforce is the sole source of truth for new quotes/opportunities. No other channel creates orders. | Duplicate orders, data integrity failure |
| A-002 | CloudSuite/Infor will always respond to activation requests with an acknowledgement BOD (success or failure) within a reasonable timeframe. | Stuck 'Requested' statuses, manual intervention required |
| A-003 | IPG APIs maintain backward compatibility — no breaking changes to quote import or activation submission endpoints. | Integration failures, data loss |
| A-004 | Users have stable network connectivity to Azure-hosted services during working hours. | Service unavailability, failed operations |
| A-005 | Division scoping at the application layer is sufficient — no database-level row-level security is required. | Data leakage if application logic has bugs |
| A-006 | The 30-minute cache TTL provides acceptable accuracy for deduplication (missed duplicates within 30-min window are tolerable). | Occasional duplicate imports |

### 9.2 Dependencies

| ID | Dependency | Type | Criticality |
|---|---|---|---|
| D-001 | Azure Service Bus — required for all async integrations (activation, change orders, agreement updates) | Infrastructure | Critical |
| D-002 | Azure Key Vault — required for all secrets management (connection strings, API keys) | Infrastructure | Critical |
| D-003 | Azure Blob Storage — required for product hierarchy data ('hierarchy' container) | Infrastructure | High |
| D-004 | Azure Table Storage — required for watermark-based incremental sync tracking | Infrastructure | High |
| D-005 | Salesforce API — required for opportunity import and user language resolution | External Service | Critical |
| D-006 | IPG Order Management API — required for activation submission and quote data retrieval | External Service | Critical |
| D-007 | IPG Order Integration API — required for agreement line updates | External Service | High |
| D-008 | IPG Pricing API — required for pricing data | External Service | Medium |
| D-009 | CloudSuite/Infor — required for activation acknowledgements and change order BOD processing | External Service | Critical |
| D-010 | Application Insights — required for telemetry, error tracking, and distributed tracing | Monitoring | Medium |

---

## 10. Risk Analysis

### 10.1 Technical Risks

| ID | Risk | Probability | Impact | Mitigation |
|---|---|---|---|---|
| R-TECH-001 | Service Bus message loss or duplication causing state inconsistency between OrderFulfillment and CloudSuite (e.g., activation request sent but never acknowledged) | Medium | Critical | Dead-letter queues, ProcessingErrors table, idempotent message handlers, monitoring alerts |
| R-TECH-002 | Fulfilment stored procedure performance degradation as data grows — FulfilSerialized/FulfilNonSerialized query across 4+ tables simultaneously | Medium | High | Composite covering indexes (already in place), regular index maintenance, query plan monitoring |
| R-TECH-003 | Cache staleness causing duplicate imports — 30-min TTL means recently-changed opportunities could be re-imported | Low | Medium | Database existence check as secondary deduplication layer |
| R-TECH-004 | CloudSuite acknowledgement timeout — if Infor doesn't respond, ActivationStatus stays at 'Requested' indefinitely | Medium | High | Monitoring alerts on stale 'Requested' statuses, manual override capability, configurable timeout threshold |

### 10.2 Integration Risks

| ID | Risk | Probability | Impact | Mitigation |
|---|---|---|---|---|
| R-INT-001 | IPG API breaking changes — external dependency with no guaranteed SLA for backward compatibility | Medium | High | API versioning strategy, contract testing, abstraction layer for IPG calls |
| R-INT-002 | Salesforce SOQL query rate limits — high-volume opportunity imports could hit API governor limits | Low | Medium | Batch processing with throttling, off-peak scheduling, monitoring of API usage |

### 10.3 Operational Risks

| ID | Risk | Probability | Impact | Mitigation |
|---|---|---|---|---|
| R-OPS-001 | AKS migration disruption — infrastructure change could affect service availability during cutover | Medium | High | Blue-green deployment strategy, rollback plan, phased migration. **Status: Complete** |
| R-OPS-002 | .NET 10 upgrade incompatibilities — framework upgrade may require significant refactoring or introduce breaking changes | Low | Medium | Incremental upgrade path, comprehensive test suite, isolated testing environment. **Status: Complete** |

### 10.4 Data Risks

| ID | Risk | Probability | Impact | Mitigation |
|---|---|---|---|---|
| R-DATA-001 | ChangeSequence ordering conflicts — concurrent BOD messages could arrive out of order, causing incorrect state | Low | High | Sequence validation on ingest, out-of-order detection logging, eventual consistency reconciliation |
| R-DATA-002 | Division scoping bypass — application-layer enforcement means a bug could expose cross-division data | Low | Critical | Regular security audits, automated access control testing, principle of least privilege in queries |

---

## 11. Glossary

| Term | Definition |
|---|---|
| BOD | Business Object Document — Infor's XML-based message format for inter-system communication between CloudSuite and other enterprise systems. |
| CloudSuite / Infor M3 | Enterprise Resource Planning (ERP) system responsible for agreement lifecycle management. Infor M3 is the ERP; CloudSuite is the cloud platform. Communicates via BOD messages over Azure Service Bus through Infor ION. |
| ION | Infor ION — the integration middleware and data lake layer that connects Infor M3/CloudSuite to external systems. Asset and product item data is sourced from the ION Data Lake. |
| IPG | Integration Platform Gateway — provides REST APIs for order management, order integration, and pricing services. |
| SOQL | Salesforce Object Query Language — used to query Salesforce for opportunities, quotes, and user data. |
| CPQ | Configure, Price, Quote — the product catalogue system containing generic templates (CPQ Generic) and specific item variants (CPQ Item) with attributes. |
| FulfilmentStatus | Integer field on Header and Line entities that maps to the `OrderAssignmentStatus` enum: 1=Unfulfilled, 2=PartiallyFulfilled, 3=FullyFulfilled, 4=Finished. |
| ActivationStatus | Enum tracking agreement activation progress: 0=TODO, 1=Failed, 2=Requested, 3=Activated. |
| Ringfence | A time-bounded constraint (FromDate, ToDate) that blocks specific assets from being reserved for fulfilment within certain divisions and warehouses. (Note: spelled as one word in codebase.) |
| Division | Organizational unit used for data scoping and access control. Standard Users can only view/manage data within their assigned division. |
| Watermark Pattern | Incremental data synchronisation mechanism using high-water mark values stored in Azure Table Storage to track last-processed records, avoiding full data reloads. |
| Header | Top-level order/agreement entity containing metadata (QuotePublicId, AgreementNumber, OrderNumber, CustomerName, Division, dates, statuses). |
| Line | Individual order item within a Header, representing a specific piece of equipment with quantity, dates, attributes, and its own fulfilment/activation status. |
| Reservation | Record linking a physical Asset to an order Line, with quantity, warehouse, and confirmation flags. |
| Asset | Physical equipment item tracked with IndividualItemNumber, status (Available/RemovedStock/Scrap/Sold), warehouse location, and telemetry. |
| Soft Delete | Data deletion strategy using an IsDeleted flag rather than physical record removal, preserving audit history and referential integrity. |
| ChangeSequence | Monotonically increasing bigint value on Headers and Lines that tracks the version ordering of post-activation change orders. |
| Service Bus Queue | Azure messaging queue for asynchronous integration. Named queues in this system: UpdateByAgreement, UpsertQuote, ActivateAgreement, ActivateHeader, ChangeNotify, ChangeApproval, ChangeComplete, AgreementSync. |
| MDP / FDP / CPQ Contexts | Separated read-optimised database contexts used to isolate catalogue queries from transactional data operations, improving performance. |
| Dead-Letter Queue | Azure Service Bus mechanism for messages that cannot be processed after maximum retry attempts, enabling manual inspection and recovery. |
| ProcessingErrors | Database table capturing failed message processing events with full error context for troubleshooting and retry operations. |
| AuditLog | Database table (with AuditLog_Staging) that captures all data changes across mutable entities for compliance and traceability. |