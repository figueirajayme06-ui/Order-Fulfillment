import type { TableColumnDefinition } from "../../lib/tableColumnLayout";

export const AGREEMENT_COLUMN_CATALOG = [
  {
    key: "fulfilmentStatus",
    labelKey: "tableColumns.fields.status",
    defaultVisible: true,
    defaultWidth: 64,
    minWidth: 56,
    maxWidth: 160,
    growWeight: 0.15,
    locked: true,
  },
  {
    key: "agreementNumber",
    labelKey: "agreements.agreementNumber",
    compactLabelKey: "tableColumns.fields.agreementNumberCompact",
    defaultVisible: true,
    defaultWidth: 104,
    minWidth: 86,
    maxWidth: 260,
    growWeight: 1,
    required: true,
    locked: true,
  },
  {
    key: "customerName",
    labelKey: "agreements.customerName",
    defaultVisible: true,
    defaultWidth: 180,
    minWidth: 140,
    maxWidth: 520,
    growWeight: 3,
  },
  {
    key: "opportunityName",
    labelKey: "tableColumns.fields.opportunity",
    defaultVisible: true,
    defaultWidth: 160,
    minWidth: 130,
    maxWidth: 480,
    growWeight: 2.5,
  },
  {
    key: "division",
    labelKey: "tableColumns.fields.division",
    defaultVisible: true,
    defaultWidth: 60,
    minWidth: 44,
    maxWidth: 150,
    growWeight: 0.3,
  },
  {
    key: "warehouse",
    labelKey: "tableColumns.fields.warehouseShort",
    defaultVisible: true,
    defaultWidth: 64,
    minWidth: 48,
    maxWidth: 190,
    growWeight: 0.5,
  },
  {
    key: "deliveryDate",
    labelKey: "tableColumns.fields.deliveryDate",
    compactLabelKey: "tableColumns.fields.deliveryDateCompact",
    defaultVisible: true,
    defaultWidth: 104,
    minWidth: 92,
    maxWidth: 240,
    growWeight: 0.6,
  },
  {
    key: "validFromDate",
    labelKey: "tableColumns.fields.validFrom",
    defaultVisible: true,
    defaultWidth: 92,
    minWidth: 84,
    maxWidth: 220,
    growWeight: 0.6,
  },
  {
    key: "validToDate",
    labelKey: "tableColumns.fields.validTo",
    defaultVisible: true,
    defaultWidth: 92,
    minWidth: 84,
    maxWidth: 220,
    growWeight: 0.6,
  },
  {
    key: "terminationDate",
    labelKey: "tableColumns.fields.terminationDate",
    compactLabelKey: "tableColumns.fields.terminationDateCompact",
    defaultVisible: true,
    defaultWidth: 112,
    minWidth: 100,
    maxWidth: 260,
    growWeight: 0.6,
  },
  {
    key: "collectionDate",
    labelKey: "tableColumns.fields.collectionDate",
    compactLabelKey: "tableColumns.fields.collectionDateCompact",
    defaultVisible: true,
    defaultWidth: 104,
    minWidth: 92,
    maxWidth: 240,
    growWeight: 0.6,
  },
  {
    key: "lastUpdatedByName",
    labelKey: "tableColumns.fields.lastUpdatedBy",
    compactLabelKey: "tableColumns.fields.lastUpdatedByCompact",
    defaultVisible: true,
    defaultWidth: 120,
    minWidth: 110,
    maxWidth: 300,
    growWeight: 1.3,
  },
  {
    key: "lineCount",
    labelKey: "tableColumns.fields.lines",
    defaultVisible: true,
    defaultWidth: 68,
    minWidth: 52,
    maxWidth: 140,
    growWeight: 0.3,
  },
  {
    key: "fromDate",
    labelKey: "tableColumns.fields.fromDate",
    defaultVisible: false,
    defaultWidth: 104,
    minWidth: 92,
    maxWidth: 240,
    growWeight: 0.6,
  },
  {
    key: "toDate",
    labelKey: "tableColumns.fields.toDate",
    defaultVisible: false,
    defaultWidth: 104,
    minWidth: 92,
    maxWidth: 240,
    growWeight: 0.6,
  },
  {
    key: "customerNumber",
    labelKey: "tableColumns.fields.customerNumber",
    defaultVisible: false,
    defaultWidth: 120,
    minWidth: 96,
    maxWidth: 260,
    growWeight: 1,
  },
  {
    key: "customerAddress",
    labelKey: "tableColumns.fields.customerAddress",
    defaultVisible: false,
    defaultWidth: 200,
    minWidth: 140,
    maxWidth: 520,
    growWeight: 2,
  },
  {
    key: "lastUpdatedDate",
    labelKey: "tableColumns.fields.updatedDate",
    defaultVisible: false,
    defaultWidth: 112,
    minWidth: 100,
    maxWidth: 260,
    growWeight: 0.6,
  },
  {
    key: "opportunityStage",
    labelKey: "tableColumns.fields.opportunityStage",
    defaultVisible: false,
    defaultWidth: 140,
    minWidth: 100,
    maxWidth: 320,
    growWeight: 1.2,
  },
  {
    key: "probability",
    labelKey: "tableColumns.fields.probability",
    defaultVisible: false,
    defaultWidth: 88,
    minWidth: 64,
    maxWidth: 180,
    growWeight: 0.4,
  },
] as const satisfies readonly TableColumnDefinition[];

export const AGREEMENT_TIMELINE_COLUMN_CATALOG = AGREEMENT_COLUMN_CATALOG.map((column) => {
  switch (column.key) {
    case "fulfilmentStatus":
      return { ...column, defaultWidth: 64, minWidth: 56, maxWidth: 96 };
    case "agreementNumber":
      return { ...column, defaultWidth: 110, minWidth: 92, maxWidth: 180 };
    case "customerName":
      return { ...column, defaultWidth: 170, minWidth: 130, maxWidth: 280 };
    case "opportunityName":
      return { ...column, defaultVisible: false, defaultWidth: 160, minWidth: 130, maxWidth: 260 };
    case "division":
      return { ...column, defaultVisible: false, defaultWidth: 64, minWidth: 52, maxWidth: 100 };
    case "warehouse":
      return { ...column, defaultWidth: 88, minWidth: 64, maxWidth: 140 };
    case "deliveryDate":
      return { ...column, defaultWidth: 104, minWidth: 92, maxWidth: 140 };
    case "lineCount":
      return { ...column, defaultVisible: false, defaultWidth: 68, minWidth: 56, maxWidth: 100 };
    default:
      return { ...column, defaultVisible: false };
  }
}) satisfies readonly TableColumnDefinition[];

export type AgreementColumnKey = (typeof AGREEMENT_COLUMN_CATALOG)[number]["key"];
export type AgreementTimelineColumnKey = (typeof AGREEMENT_TIMELINE_COLUMN_CATALOG)[number]["key"];
