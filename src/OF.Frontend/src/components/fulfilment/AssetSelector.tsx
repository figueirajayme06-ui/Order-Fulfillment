import { useCallback, useEffect, useId, useMemo, useRef, useState, type FC } from "react";
import { useTranslation } from "react-i18next";
import { fetchAssets, type AssetFilterParams } from "../../services/assetsService";
import type { Asset } from "../../types";
import { Button } from "../common";
import { AssetFilterBar, type AssetFilterValues } from "./AssetFilterBar";
import styles from "./AssetSelector.module.css";

type SortField = "id" | "itemNumber" | "warehouse" | "status" | "description";
type SortDirection = "asc" | "desc";

interface AssetSelectorProps {
  warehouse?: string;
  division?: string;
  itemNumber?: string;
  restoreFocusTo?: HTMLElement | null;
  onSelect: (asset: Asset) => void;
  onCancel: () => void;
}

function getStatusClass(status: string | null): string {
  switch (status) {
    case "Available":
      return styles.statusAvailable;
    case "OnHire":
      return styles.statusOnHire;
    case "Service":
    case "Repair":
      return styles.statusService;
    case "Collection":
    case "In Transit":
      return styles.statusTransit;
    default:
      return "";
  }
}

const EMPTY_FILTERS: AssetFilterValues = {
  search: "",
  status: "",
  warehouse: "",
  division: "",
  itemNumber: "",
  description: "",
};

const FOCUSABLE_SELECTOR = [
  "button:not([disabled])",
  "[href]",
  "input:not([disabled]):not([type='hidden'])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  "[tabindex]:not([tabindex='-1'])",
].join(",");

function isVisibleFocusable(element: HTMLElement): boolean {
  const rect = element.getBoundingClientRect();
  return rect.width > 0 && rect.height > 0;
}

export const AssetSelector: FC<AssetSelectorProps> = ({
  warehouse,
  division,
  itemNumber,
  restoreFocusTo,
  onSelect,
  onCancel,
}) => {
  const { t } = useTranslation();
  const dialogTitleId = useId();
  // Pre-populate context fields as refinement options, but put itemNumber into
  // the dedicated itemNumber filter so the auto-search is not over-restricted.
  // CPQ generic codes are prefixed with "XG" (e.g. XGGN0200), while M3 asset item
  // numbers are not (e.g. GN0200GHPCAN). Strip the leading "XG" to bridge the two systems.
  const m3ItemNumber = itemNumber?.startsWith("XG") ? itemNumber.slice(2) : (itemNumber ?? "");
  const [filters, setFilters] = useState<AssetFilterValues>({
    ...EMPTY_FILTERS,
    warehouse: warehouse ?? "",
    division: division ?? "",
    itemNumber: m3ItemNumber,
  });
  const [assets, setAssets] = useState<Asset[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [sortField, setSortField] = useState<SortField>("id");
  const [sortDirection, setSortDirection] = useState<SortDirection>("asc");
  const autoSearched = useRef(false);
  const modalRef = useRef<HTMLDivElement>(null);
  const previousFocusedElementRef = useRef<HTMLElement | null>(null);

  const getFocusableElements = useCallback(() => {
    if (!modalRef.current) {
      return [] as HTMLElement[];
    }

    return Array.from(modalRef.current.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)).filter(
      (element) => !element.hasAttribute("disabled") && isVisibleFocusable(element),
    );
  }, []);

  const doSearch = useCallback(async () => {
    const hasAnyFilter = Object.values(filters).some((v) => v.trim() !== "");
    if (!hasAnyFilter) return;

    setIsLoading(true);
    try {
      const params: AssetFilterParams = {
        search: filters.search || undefined,
        status: filters.status || undefined,
        warehouse: filters.warehouse || undefined,
        division: filters.division || undefined,
        itemNumber: filters.itemNumber || undefined,
        description: filters.description || undefined,
        excludeStatuses: "RemovedStock,Scrap,Sold",
        take: 100,
      };
      const results = await fetchAssets(params);
      setAssets(results);
    } catch {
      setAssets([]);
    } finally {
      setIsLoading(false);
    }
  }, [filters]);

  // Auto-search on open using only itemNumber — intentionally excludes warehouse/division
  // so the user sees all assets of this type before optionally narrowing by location.
  // CPQ generic codes are prefixed with "X" (e.g. XGGN0200), while M3 asset item
  // numbers are not (e.g. GN0200GHPCAN). Strip the leading "XG" to bridge the two systems.
  const doAutoSearch = useCallback(async (searchItemNumber: string) => {
    const m3Code = searchItemNumber.startsWith("XG") ? searchItemNumber.slice(2) : searchItemNumber;
    setIsLoading(true);
    try {
      const results = await fetchAssets({
        itemNumber: m3Code,
        excludeStatuses: "RemovedStock,Scrap,Sold",
        take: 100,
      });
      setAssets(results);
    } catch {
      setAssets([]);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (itemNumber && !autoSearched.current) {
      autoSearched.current = true;
      doAutoSearch(itemNumber);
    }
  }, [itemNumber, doAutoSearch]);

  useEffect(() => {
    previousFocusedElementRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;

    const rafId = window.requestAnimationFrame(() => {
      const focusables = getFocusableElements();
      if (focusables.length > 0) {
        focusables[0].focus();
      } else {
        modalRef.current?.focus();
      }
    });

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onCancel();
        return;
      }

      if (event.key !== "Tab") {
        return;
      }

      const focusables = getFocusableElements();

      if (focusables.length === 0) {
        event.preventDefault();
        modalRef.current?.focus();
        return;
      }

      const first = focusables[0];
      const last = focusables[focusables.length - 1];
      const active = document.activeElement;

      if (event.shiftKey) {
        if (active === first || !(active instanceof Node) || !modalRef.current?.contains(active)) {
          event.preventDefault();
          last.focus();
        }
        return;
      }

      if (active === last) {
        event.preventDefault();
        first.focus();
      }
    };

    document.addEventListener("keydown", handleKeyDown);

    return () => {
      window.cancelAnimationFrame(rafId);
      document.removeEventListener("keydown", handleKeyDown);

      const restoreTarget = restoreFocusTo ?? previousFocusedElementRef.current;
      if (restoreTarget && document.contains(restoreTarget)) {
        restoreTarget.focus();
      }
    };
  }, [getFocusableElements, onCancel, restoreFocusTo]);

  const handleFilterChange = useCallback((field: keyof AssetFilterValues, value: string) => {
    setFilters((prev) => ({ ...prev, [field]: value }));
  }, []);

  const handleClear = useCallback(() => {
    setFilters({ ...EMPTY_FILTERS });
    setAssets([]);
  }, []);

  const handleSort = useCallback((field: SortField) => {
    setSortField((prev) => {
      if (prev === field) {
        setSortDirection((d) => (d === "asc" ? "desc" : "asc"));
        return prev;
      }
      setSortDirection("asc");
      return field;
    });
  }, []);

  const sortedAssets = useMemo(() => {
    return [...assets].sort((a, b) => {
      const aVal = (a[sortField] ?? "") as string;
      const bVal = (b[sortField] ?? "") as string;
      const cmp = aVal.localeCompare(bVal, undefined, { numeric: true, sensitivity: "base" });
      return sortDirection === "asc" ? cmp : -cmp;
    });
  }, [assets, sortField, sortDirection]);

  const sortIndicator = (field: SortField) => {
    if (sortField !== field) return "";
    return sortDirection === "asc" ? " ▲" : " ▼";
  };

  return (
    <div className={styles.overlay} onClick={onCancel} role="presentation">
      <div
        className={styles.modal}
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby={dialogTitleId}
        tabIndex={-1}
        ref={modalRef}
      >
        <div className={styles.header}>
          <h3 id={dialogTitleId}>Select Asset</h3>
          {(warehouse || division) && (
            <span className={styles.contextHint}>{[warehouse, division].filter(Boolean).join(" · ")}</span>
          )}
          <button type="button" className={styles.close} onClick={onCancel} aria-label="Close asset selector">
            ×
          </button>
        </div>

        <AssetFilterBar
          values={filters}
          onChange={handleFilterChange}
          onSearch={doSearch}
          onClear={handleClear}
          isLoading={isLoading}
        />

        <div className={styles.results}>
          {isLoading ? (
            <p className={styles.status}>Searching...</p>
          ) : assets.length === 0 ? (
            <p className={styles.status}>
              {Object.values(filters).some((v) => v) ? t("common.noResults") : "Use filters above to search for assets"}
            </p>
          ) : (
            <>
              <p className={styles.resultCount}>
                {assets.length} asset{assets.length !== 1 ? "s" : ""} found
              </p>
              <table className={styles.table}>
                <thead>
                  <tr>
                    <th className={styles.sortable} onClick={() => handleSort("id")}>
                      Asset ID{sortIndicator("id")}
                    </th>
                    <th className={styles.sortable} onClick={() => handleSort("itemNumber")}>
                      Item #{sortIndicator("itemNumber")}
                    </th>
                    <th className={styles.sortable} onClick={() => handleSort("description")}>
                      Description{sortIndicator("description")}
                    </th>
                    <th className={styles.sortable} onClick={() => handleSort("warehouse")}>
                      Warehouse{sortIndicator("warehouse")}
                    </th>
                    <th className={styles.sortable} onClick={() => handleSort("status")}>
                      Status{sortIndicator("status")}
                    </th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {sortedAssets.map((asset) => (
                    <tr key={asset.id}>
                      <td className={styles.mono}>{asset.id}</td>
                      <td>{asset.itemNumber ?? "—"}</td>
                      <td className={styles.descCell}>{asset.description ?? "—"}</td>
                      <td>{asset.warehouse ?? "—"}</td>
                      <td>
                        <span className={`${styles.statusBadge} ${getStatusClass(asset.status)}`}>
                          {asset.status ?? "—"}
                        </span>
                      </td>
                      <td>
                        <Button label="Select" variant="primary" size="small" onClick={() => onSelect(asset)} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}
        </div>
      </div>
    </div>
  );
};
