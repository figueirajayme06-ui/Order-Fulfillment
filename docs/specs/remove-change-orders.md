# Future feature: remove Change Orders

> **Status:** Future task. This is independent of reservation clash
> notifications and requires discovery before implementation.

## 1. Objective

Remove the Change Orders feature from Order Fulfillment, including its unused or
legacy application, integration, permission, and persistence paths. The removal
must not affect normal Quote, T Agreement, A Agreement, M3, or Salesforce quote
processing.

## 2. Planned scope

- Inventory every Change Order route, controller, view, model, repository method,
  role, feature flag, queue/function handler, Salesforce query/model, test, and
  document.
- Confirm that no supported workflow or external producer still depends on the
  Change Order queues or endpoints.
- Remove the user-facing feature and its navigation and permissions.
- Remove inactive Change Order processing code and configuration from `OF.UI`,
  `OF.Api`, `OF.Common`, and related deployment assets.
- Remove persistence mappings and schema only after historical-data retention
  and rollback requirements are agreed.
- Update architecture, API, operational, and support documentation.

## 3. Boundaries and safeguards

- Do not include Change Orders in reservation clash detection.
- Do not delete historical Change Order data until retention or archival has
  explicit approval.
- Do not remove shared Salesforce or M3 integration code used by normal agreement
  and quote workflows.
- Treat queue, subscription, feature-flag, role, and database removal as staged
  deployment changes so producers can be disabled before consumers and schema
  are removed.

## 4. Suggested delivery order

1. Complete the dependency and runtime-usage inventory.
2. Confirm external ownership, data retention, and rollback requirements.
3. Disable entry points and external producers.
4. Remove application and integration code, roles, flags, and tests.
5. Remove deployment resources and configuration after observing the disabled
   workflow.
6. Archive or remove persistence data and schema under an approved migration.
7. Run the full build and regression suite for agreement, reservation, M3, and
   Salesforce quote workflows.

## 5. Acceptance criteria

- [ ] No Change Order navigation, route, API, background handler, role, or feature
  flag remains active.
- [ ] No external producer sends Change Order messages to Order Fulfillment.
- [ ] Required historical data is retained or archived according to the agreed
  policy.
- [ ] Quote, T Agreement, A Agreement, reservation, M3, and Salesforce quote
  workflows continue to operate.
- [ ] Application, integration, deployment, test, and support documentation no
  longer describes Change Orders as a supported feature.

## 6. Decisions required before implementation

- Historical-data retention or archival period.
- Ownership and shutdown sequence for external Salesforce and Service Bus
  producers.
- Whether database tables are retained read-only for a period or removed in the
  same release.
- Whether this work is completed before or as part of final `OF.UI`
  decommissioning.
