import type { Dispatch, SetStateAction } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";
import { Badge, Button, Spinner, TableColumnHeader } from "../../components/common";
import type { RingfenceAsset, RingfenceDetail } from "../../services/ringfenceService";
import { assetStatusVariant, type AssetSortField, type SortDirection } from "./ringfenceModel";
import styles from "./RingfencePage.module.css";

interface RingfenceAssetsProps {
  currentAssetCount: number;
  canMutate: boolean;
  isAssetEntryOpen: boolean;
  setIsAssetEntryOpen: Dispatch<SetStateAction<boolean>>;
  selectedDetail: RingfenceDetail | null;
  isDetailLoading: boolean;
  assetIds: string;
  setAssetIds: (value: string) => void;
  isAddingAssets: boolean;
  requestAddAssets: () => Promise<void>;
  chooseFromAssets: () => void;
  detailError: boolean;
  onRetry: () => void;
  assetSearch: string;
  setAssetSearch: (value: string) => void;
  assetStatusFilter: string;
  setAssetStatusFilter: (value: string) => void;
  assetStatusOptions: readonly string[];
  assetSortField: AssetSortField;
  assetSortDirection: SortDirection;
  toggleAssetSort: (field: AssetSortField) => void;
  visibleAssets: readonly RingfenceAsset[];
  onRemoveAsset: (asset: RingfenceAsset) => void;
  totalAssetPages: number;
  assetPage: number;
  setAssetPage: Dispatch<SetStateAction<number>>;
}

export function RingfenceAssets({
  currentAssetCount,
  canMutate,
  isAssetEntryOpen,
  setIsAssetEntryOpen,
  selectedDetail,
  isDetailLoading,
  assetIds,
  setAssetIds,
  isAddingAssets,
  requestAddAssets,
  chooseFromAssets,
  detailError,
  onRetry,
  assetSearch,
  setAssetSearch,
  assetStatusFilter,
  setAssetStatusFilter,
  assetStatusOptions,
  assetSortField,
  assetSortDirection,
  toggleAssetSort,
  visibleAssets,
  onRemoveAsset,
  totalAssetPages,
  assetPage,
  setAssetPage,
}: RingfenceAssetsProps) {
  const { t } = useTranslation();
  return (
    <div className={styles.assetSection}>
      <header className={styles.assetSectionHeader}>
        <div>
          <h3 id="ringfence-assets-heading">{t("ringfence.protectedAssets", { count: currentAssetCount })}</h3>
        </div>
        {canMutate && (
          <div className={styles.assetSectionActions} data-print-hidden>
            <Button
              label={isAssetEntryOpen ? t("ringfence.closeAddAssets") : t("ringfence.addAssets")}
              size="small"
              onClick={() => setIsAssetEntryOpen((isOpen) => !isOpen)}
              disabled={!selectedDetail || isDetailLoading}
            />
          </div>
        )}
      </header>

      {canMutate && isAssetEntryOpen && selectedDetail && (
        <div className={styles.assetEntry} data-print-hidden>
          <label htmlFor="ringfence-assets">{t("ringfence.pasteAssetIds")}</label>
          <textarea
            id="ringfence-assets"
            value={assetIds}
            onChange={(event) => setAssetIds(event.target.value)}
            placeholder={t("ringfence.assetHint")}
          />
          <div className={styles.assetEntryActions}>
            <Button
              label={isAddingAssets ? t("ringfence.addingAssets") : t("ringfence.addAssetIds")}
              size="small"
              onClick={() => void requestAddAssets()}
              disabled={!assetIds.trim() || isAddingAssets}
            />
            <Button
              label={t("ringfence.chooseFromAssets")}
              variant="secondary"
              size="small"
              onClick={chooseFromAssets}
              disabled={isAddingAssets}
            />
          </div>
        </div>
      )}

      {isDetailLoading && !selectedDetail ? (
        <div className={styles.loadingArea}>
          <Spinner />
        </div>
      ) : detailError ? (
        <div className={styles.emptyState}>
          <p>{t("ringfence.detailError")}</p>
          <Button label={t("common.retry")} variant="secondary" size="small" onClick={() => onRetry()} />
        </div>
      ) : !selectedDetail || selectedDetail.assets.length === 0 ? (
        <div className={styles.assetEmpty}>
          <p>{t("ringfence.noAssets")}</p>
          {canMutate && !isAssetEntryOpen && (
            <Button label={t("ringfence.addAssets")} size="small" onClick={() => setIsAssetEntryOpen(true)} />
          )}
        </div>
      ) : (
        <>
          <div className={styles.assetFilters} data-print-hidden>
            <label className={styles.searchField}>
              <span className={styles.srOnly}>{t("ringfence.searchAssets")}</span>
              <input
                type="search"
                value={assetSearch}
                onChange={(event) => setAssetSearch(event.target.value)}
                placeholder={t("ringfence.searchAssets")}
              />
            </label>
            <select
              value={assetStatusFilter}
              onChange={(event) => setAssetStatusFilter(event.target.value)}
              aria-label={t("ringfence.filterAssetStatus")}
            >
              <option value="">{t("ringfence.allAssetStatuses")}</option>
              {assetStatusOptions.map((status) => (
                <option key={status} value={status}>
                  {status}
                </option>
              ))}
            </select>
          </div>
          <div className={styles.assetTableWrap}>
            <table
              className={styles.assetTable}
              aria-labelledby="ringfence-assets-heading"
              data-print-table="ringfence-assets"
            >
              <thead>
                <tr>
                  <th>
                    <TableColumnHeader
                      label={t("ringfence.assetId")}
                      sortDirection={assetSortField === "id" ? assetSortDirection : undefined}
                      onSort={() => toggleAssetSort("id")}
                    />
                  </th>
                  <th>
                    <TableColumnHeader
                      label={t("ringfence.status")}
                      sortDirection={assetSortField === "status" ? assetSortDirection : undefined}
                      onSort={() => toggleAssetSort("status")}
                    />
                  </th>
                  <th>
                    <TableColumnHeader
                      label={t("ringfence.asset")}
                      sortDirection={assetSortField === "description" ? assetSortDirection : undefined}
                      onSort={() => toggleAssetSort("description")}
                    />
                  </th>
                  <th>{t("ringfence.location")}</th>
                  <th>{t("ringfence.division")}</th>
                  {canMutate && <th data-print-hidden>{t("common.actions")}</th>}
                </tr>
              </thead>
              <tbody>
                {visibleAssets.map((asset) => (
                  <tr key={asset.id}>
                    <td>
                      <Link className={styles.assetLink} to={"/assets/" + encodeURIComponent(asset.id)}>
                        {asset.id}
                      </Link>
                    </td>
                    <td>
                      <Badge label={asset.status ?? "—"} variant={assetStatusVariant(asset.status)} />
                    </td>
                    <td>
                      <div className={styles.assetIdentity}>
                        <span>{asset.description ?? "—"}</span>
                        {asset.itemNumber && <span>{asset.itemNumber}</span>}
                      </div>
                    </td>
                    <td>
                      <div className={styles.assetIdentity}>
                        <span>{asset.warehouse ?? "—"}</span>
                        {asset.warehouseLocation && <span>{asset.warehouseLocation}</span>}
                      </div>
                    </td>
                    <td>{asset.division ?? "—"}</td>
                    {canMutate && (
                      <td data-print-hidden>
                        <button
                          type="button"
                          className={styles.removeAssetButton}
                          onClick={() => selectedDetail && onRemoveAsset(asset)}
                          disabled={isAddingAssets}
                          aria-label={t("ringfence.removeAsset", { assetId: asset.id })}
                        >
                          {t("common.remove")}
                        </button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {totalAssetPages > 1 && (
            <nav className={styles.pagination} aria-label={t("ringfence.assetPagination")} data-print-hidden>
              <Button
                label={t("common.previous")}
                variant="secondary"
                size="small"
                onClick={() => setAssetPage((page) => Math.max(1, page - 1))}
                disabled={assetPage === 1}
              />
              <span>{t("ringfence.pageOf", { page: assetPage, total: totalAssetPages })}</span>
              <Button
                label={t("common.next")}
                variant="secondary"
                size="small"
                onClick={() => setAssetPage((page) => Math.min(totalAssetPages, page + 1))}
                disabled={assetPage === totalAssetPages}
              />
            </nav>
          )}
        </>
      )}
    </div>
  );
}
