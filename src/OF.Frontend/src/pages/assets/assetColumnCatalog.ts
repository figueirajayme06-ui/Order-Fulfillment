import type { TableColumnDefinition } from "../../lib/tableColumnLayout";

export const ASSET_COLUMN_CATALOG = [
  {
    key: "status",
    labelKey: "tableColumns.fields.status",
    defaultVisible: true,
    defaultWidth: 96,
    minWidth: 72,
    maxWidth: 180,
    growWeight: 0.6,
    locked: true,
  },
  {
    key: "id",
    labelKey: "tableColumns.fields.assetId",
    defaultVisible: true,
    defaultWidth: 105,
    minWidth: 84,
    maxWidth: 240,
    growWeight: 1,
    required: true,
    locked: true,
  },
  {
    key: "itemNumber",
    labelKey: "tableColumns.fields.itemNumberShort",
    filterLabelKey: "tableColumns.fields.itemNumber",
    defaultVisible: true,
    defaultWidth: 95,
    minWidth: 72,
    maxWidth: 220,
    growWeight: 1.2,
  },
  {
    key: "description",
    labelKey: "tableColumns.fields.description",
    defaultVisible: true,
    defaultWidth: 160,
    minWidth: 120,
    maxWidth: 480,
    growWeight: 3,
  },
  {
    key: "warehouse",
    labelKey: "tableColumns.fields.warehouseShort",
    filterLabelKey: "tableColumns.fields.warehouse",
    defaultVisible: true,
    defaultWidth: 75,
    minWidth: 48,
    maxWidth: 190,
    growWeight: 0.5,
  },
  {
    key: "division",
    labelKey: "tableColumns.fields.division",
    filterLabelKey: "agreements.division",
    defaultVisible: true,
    defaultWidth: 55,
    minWidth: 44,
    maxWidth: 150,
    growWeight: 0.3,
  },
  {
    key: "customerName",
    labelKey: "tableColumns.fields.customer",
    defaultVisible: true,
    defaultWidth: 150,
    minWidth: 110,
    maxWidth: 360,
    growWeight: 2.2,
  },
  {
    key: "deliveryDate",
    labelKey: "tableColumns.fields.deliveryDateCompact",
    filterLabelKey: "tableColumns.fields.deliveryDate",
    defaultVisible: true,
    defaultWidth: 110,
    minWidth: 92,
    maxWidth: 240,
    growWeight: 0.6,
  },
  {
    key: "agreementLineValidFromDate",
    labelKey: "tableColumns.fields.validFrom",
    defaultVisible: true,
    defaultWidth: 110,
    minWidth: 84,
    maxWidth: 220,
    growWeight: 0.6,
  },
  {
    key: "agreementLineValidToDate",
    labelKey: "tableColumns.fields.validTo",
    defaultVisible: true,
    defaultWidth: 110,
    minWidth: 84,
    maxWidth: 220,
    growWeight: 0.6,
  },
  {
    key: "terminationDate",
    labelKey: "tableColumns.fields.termination",
    filterLabelKey: "tableColumns.fields.terminationDate",
    defaultVisible: true,
    defaultWidth: 110,
    minWidth: 92,
    maxWidth: 240,
    growWeight: 0.6,
  },
  {
    key: "collectionDate",
    labelKey: "tableColumns.fields.collection",
    filterLabelKey: "tableColumns.fields.collectionDate",
    defaultVisible: true,
    defaultWidth: 110,
    minWidth: 92,
    maxWidth: 240,
    growWeight: 0.6,
  },
  {
    key: "daysOffHire",
    labelKey: "tableColumns.fields.daysOffHireShort",
    filterLabelKey: "tableColumns.fields.daysOffHire",
    defaultVisible: true,
    defaultWidth: 62,
    minWidth: 48,
    maxWidth: 140,
    growWeight: 0.3,
  },
  {
    key: "warehouseName",
    labelKey: "tableColumns.fields.warehouseName",
    defaultVisible: false,
    defaultWidth: 160,
    minWidth: 110,
    maxWidth: 360,
    growWeight: 1.6,
  },
  {
    key: "agreementNumber",
    labelKey: "tableColumns.fields.agreement",
    defaultVisible: false,
    defaultWidth: 112,
    minWidth: 88,
    maxWidth: 260,
    growWeight: 1,
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
    key: "facility",
    labelKey: "tableColumns.fields.facility",
    defaultVisible: false,
    defaultWidth: 100,
    minWidth: 80,
    maxWidth: 220,
    growWeight: 0.8,
  },
  {
    key: "warehouseLocation",
    labelKey: "tableColumns.fields.warehouseLocation",
    defaultVisible: false,
    defaultWidth: 140,
    minWidth: 100,
    maxWidth: 320,
    growWeight: 1.1,
  },
  {
    key: "estimatedReadyDate",
    labelKey: "tableColumns.fields.readyDate",
    defaultVisible: false,
    defaultWidth: 112,
    minWidth: 100,
    maxWidth: 260,
    growWeight: 0.6,
  },
  {
    key: "productGroup",
    labelKey: "tableColumns.fields.productGroup",
    defaultVisible: false,
    defaultWidth: 140,
    minWidth: 100,
    maxWidth: 320,
    growWeight: 1.1,
  },
  {
    key: "productCategory",
    labelKey: "tableColumns.fields.productCategory",
    defaultVisible: false,
    defaultWidth: 150,
    minWidth: 110,
    maxWidth: 360,
    growWeight: 1.2,
  },
  {
    key: "runHours",
    labelKey: "tableColumns.fields.runHours",
    defaultVisible: false,
    defaultWidth: 96,
    minWidth: 72,
    maxWidth: 180,
    growWeight: 0.5,
  },
  {
    key: "size",
    labelKey: "tableColumns.fields.size",
    defaultVisible: false,
    defaultWidth: 90,
    minWidth: 68,
    maxWidth: 180,
    growWeight: 0.5,
  },
  {
    key: "telemetryStatus",
    labelKey: "tableColumns.fields.telemetry",
    defaultVisible: false,
    defaultWidth: 110,
    minWidth: 80,
    maxWidth: 240,
    growWeight: 0.8,
  },
  {
    key: "remark",
    labelKey: "tableColumns.fields.remarks",
    defaultVisible: false,
    defaultWidth: 180,
    minWidth: 120,
    maxWidth: 480,
    growWeight: 1.8,
  },
] as const satisfies readonly TableColumnDefinition[];

export const ASSET_TIMELINE_COLUMN_CATALOG = ASSET_COLUMN_CATALOG.map((column) => {
  switch (column.key) {
    case "status":
      return { ...column, defaultWidth: 88, minWidth: 72, maxWidth: 120 };
    case "id":
      return { ...column, defaultWidth: 104, minWidth: 88, maxWidth: 160 };
    case "itemNumber":
      return { ...column, defaultWidth: 96, minWidth: 76, maxWidth: 150 };
    case "description":
      return { ...column, defaultVisible: false, defaultWidth: 150, minWidth: 120, maxWidth: 260 };
    case "warehouse":
      return { ...column, defaultWidth: 92, minWidth: 64, maxWidth: 140 };
    case "division":
      return { ...column, defaultVisible: false, defaultWidth: 64, minWidth: 52, maxWidth: 100 };
    case "customerName":
      return { ...column, defaultVisible: false, defaultWidth: 140, minWidth: 110, maxWidth: 240 };
    default:
      return { ...column, defaultVisible: false };
  }
}) satisfies readonly TableColumnDefinition[];

export type AssetColumnKey = (typeof ASSET_COLUMN_CATALOG)[number]["key"];
export type AssetTimelineColumnKey = (typeof ASSET_TIMELINE_COLUMN_CATALOG)[number]["key"];
