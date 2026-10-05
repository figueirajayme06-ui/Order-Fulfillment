import type { FC, FormEvent, ReactNode } from "react";
import { Button } from "../../components/common";
import type { DivisionLookup, UserLookup, WarehouseLookup } from "../../services/lookupsService";
import type { RingfenceInput } from "../../services/ringfenceService";
import {
  groupWarehousesByDivision,
  isRingfenceWarehouse,
  sameText,
  type FormErrors,
  type WarehouseOptionGroup,
} from "./ringfenceModel";
import styles from "./RingfencePage.module.css";

interface RingfenceEditorProps {
  form: RingfenceInput;
  errors: FormErrors;
  divisionOptions: readonly DivisionLookup[];
  formDivisionCodes: readonly string[];
  canEditDivisions: boolean;
  warehouseOptions: readonly WarehouseLookup[];
  ownerOptions: readonly UserLookup[];
  isLoadingOptions: boolean;
  lookupsError: boolean;
  isSaving: boolean;
  isCreateMode: boolean;
  allowBlankWarehouse: boolean;
  minDate?: string;
  onCancel: () => void;
  onDivisionsChange: (codes: string[]) => void;
  onFieldChange: <K extends keyof RingfenceInput>(key: K, value: RingfenceInput[K]) => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  t: (key: string, options?: Record<string, unknown>) => string;
}

export const RingfenceEditor: FC<RingfenceEditorProps> = ({
  form,
  errors,
  divisionOptions,
  formDivisionCodes,
  canEditDivisions,
  warehouseOptions,
  ownerOptions,
  isLoadingOptions,
  lookupsError,
  isSaving,
  isCreateMode,
  allowBlankWarehouse,
  minDate,
  onCancel,
  onDivisionsChange,
  onFieldChange,
  onSubmit,
  t,
}) => {
  const warehouseGroups = groupWarehousesByDivision(warehouseOptions);
  const showWarehouseGroups = warehouseGroups.length > 1;
  const hasNoCreateWarehouses =
    isCreateMode && Boolean(form.divisions) && !isLoadingOptions && !lookupsError && warehouseOptions.length === 0;

  return (
    <form className={styles.editor} onSubmit={onSubmit} noValidate>
      <header className={styles.inspectorHeader}>
        <h2 id="ringfence-inspector-heading">{isCreateMode ? t("ringfence.create") : t("ringfence.editDetails")}</h2>
      </header>
      <div className={styles.editorBody}>
        <FieldError id="ringfence-title-error" className={styles.fieldWide} message={errors.title}>
          <label htmlFor="ringfence-title">{t("ringfence.name")}</label>
          <input
            id="ringfence-title"
            value={form.title}
            required
            aria-invalid={Boolean(errors.title)}
            aria-describedby={errors.title ? "ringfence-title-error" : undefined}
            onChange={(event) => onFieldChange("title", event.target.value)}
          />
        </FieldError>
        <FieldError id="ringfence-from-date-error" message={errors.fromDate}>
          <label htmlFor="ringfence-from-date">{t("ringfence.fromDate")}</label>
          <input
            id="ringfence-from-date"
            type="date"
            value={form.fromDate}
            min={minDate}
            required
            aria-invalid={Boolean(errors.fromDate)}
            aria-describedby={errors.fromDate ? "ringfence-from-date-error" : undefined}
            onChange={(event) => onFieldChange("fromDate", event.target.value)}
          />
        </FieldError>
        <FieldError id="ringfence-to-date-error" message={errors.toDate}>
          <label htmlFor="ringfence-to-date">{t("ringfence.toDate")}</label>
          <input
            id="ringfence-to-date"
            type="date"
            value={form.toDate}
            min={minDate || form.fromDate || undefined}
            required
            aria-invalid={Boolean(errors.toDate)}
            aria-describedby={errors.toDate ? "ringfence-to-date-error" : undefined}
            onChange={(event) => onFieldChange("toDate", event.target.value)}
          />
        </FieldError>
        <FieldError id="ringfence-divisions-error" className={styles.fieldWide} message={errors.divisions}>
          {canEditDivisions ? (
            <>
              <label htmlFor="ringfence-divisions">{t("ringfence.divisions")}</label>
              <select
                id="ringfence-divisions"
                className={styles.divisionSelect}
                multiple
                size={Math.min(Math.max(3, divisionOptions.length), 6)}
                value={formDivisionCodes}
                required
                aria-invalid={Boolean(errors.divisions)}
                aria-describedby={"ringfence-divisions-help" + (errors.divisions ? " ringfence-divisions-error" : "")}
                onChange={(event) =>
                  onDivisionsChange(Array.from(event.currentTarget.selectedOptions, (option) => option.value))
                }
              >
                {divisionOptions.map((division) => (
                  <option key={division.code} value={division.code}>
                    {division.code} — {division.name}
                  </option>
                ))}
              </select>
              <span id="ringfence-divisions-help" className={styles.helpText}>
                {t("ringfence.divisionSelectHint")}
              </span>
            </>
          ) : (
            <>
              <span className={styles.fieldLabel}>{t("ringfence.divisions")}</span>
              <div
                id="ringfence-divisions"
                className={styles.readOnlyField}
                role="textbox"
                tabIndex={0}
                aria-readonly="true"
                aria-label={t("ringfence.divisions")}
                aria-describedby={"ringfence-divisions-help" + (errors.divisions ? " ringfence-divisions-error" : "")}
              >
                {formDivisionCodes.join(", ")}
              </div>
              <span id="ringfence-divisions-help" className={styles.helpText}>
                {t("ringfence.divisionAccessRestricted")}
              </span>
            </>
          )}
        </FieldError>
        <FieldError id="ringfence-owner-error" message={errors.owner}>
          <label htmlFor="ringfence-owner">{t("ringfence.owner")}</label>
          <select
            id="ringfence-owner"
            value={form.owner ?? ""}
            required
            disabled={!form.divisions || isLoadingOptions}
            aria-invalid={Boolean(errors.owner)}
            aria-describedby={errors.owner ? "ringfence-owner-error" : undefined}
            onChange={(event) => onFieldChange("owner", event.target.value)}
          >
            <option value="">{isLoadingOptions ? t("ringfence.loadingOwners") : t("ringfence.selectOwner")}</option>
            {form.owner && !ownerOptions.some((owner) => owner.loginName === form.owner) && (
              <option value={form.owner}>{form.owner}</option>
            )}
            {ownerOptions.map((owner) => (
              <option key={owner.loginName} value={owner.loginName}>
                {owner.fullName ? owner.fullName + " — " + owner.loginName : owner.loginName}
              </option>
            ))}
          </select>
        </FieldError>
        <FieldError id="ringfence-warehouse-error" message={errors.warehouse}>
          <label htmlFor="ringfence-warehouse">{t("ringfence.warehouse")}</label>
          <select
            id="ringfence-warehouse"
            value={form.warehouse ?? ""}
            required={!allowBlankWarehouse}
            disabled={!form.divisions || isLoadingOptions || hasNoCreateWarehouses}
            aria-invalid={Boolean(errors.warehouse)}
            aria-describedby={
              [
                errors.warehouse ? "ringfence-warehouse-error" : null,
                hasNoCreateWarehouses ? "ringfence-no-warehouses" : null,
              ]
                .filter(Boolean)
                .join(" ") || undefined
            }
            onChange={(event) => onFieldChange("warehouse", event.target.value)}
          >
            <option value="">
              {isLoadingOptions ? t("ringfence.loadingWarehouses") : t("ringfence.selectWarehouse")}
            </option>
            {form.warehouse &&
              (!warehouseOptions.some((warehouse) => sameText(warehouse.warehouseCode, form.warehouse ?? "")) ||
                !isRingfenceWarehouse({ warehouseCode: form.warehouse })) && (
                <option value={form.warehouse}>
                  {t("ringfence.retainedLegacyWarehouse", { warehouse: form.warehouse })}
                </option>
              )}
            {showWarehouseGroups && <WarehouseOptionGroups groups={warehouseGroups} />}
            {!showWarehouseGroups &&
              warehouseOptions.map((warehouse) => (
                <option key={warehouse.warehouseCode} value={warehouse.warehouseCode}>
                  {warehouse.warehouseCode} — {warehouse.warehouse}
                </option>
              ))}
          </select>
        </FieldError>
        {hasNoCreateWarehouses && (
          <p id="ringfence-no-warehouses" className={styles.lookupError} role="status">
            {t("ringfence.noEligibleWarehouses")}
          </p>
        )}
        {lookupsError && (
          <p className={styles.lookupError} role="alert">
            {t("ringfence.lookupError")}
          </p>
        )}
      </div>
      <footer className={styles.editorFooter} data-print-hidden>
        <Button
          label={isSaving ? t("ringfence.saving") : t("common.saveChanges")}
          type="submit"
          disabled={isSaving || hasNoCreateWarehouses}
        />
        <Button label={t("common.cancel")} variant="secondary" onClick={onCancel} disabled={isSaving} />
      </footer>
    </form>
  );
};

export const WarehouseOptionGroups: FC<{ groups: readonly WarehouseOptionGroup[] }> = ({ groups }) => (
  <>
    {groups.map((group) => (
      <optgroup key={group.divisionCode} label={group.label}>
        {group.warehouses.map((warehouse) => (
          <option key={warehouse.warehouseCode} value={warehouse.warehouseCode}>
            {warehouse.warehouseCode} — {warehouse.warehouse}
          </option>
        ))}
      </optgroup>
    ))}
  </>
);

const FieldError: FC<{ id: string; className?: string; message?: string; children: ReactNode }> = ({
  id,
  className,
  message,
  children,
}) => (
  <div className={`${styles.field} ${className ?? ""}`}>
    {children}
    {message && (
      <span id={id} className={styles.fieldError} role="alert">
        {message}
      </span>
    )}
  </div>
);

export function focusFirstInvalidField(errors: FormErrors) {
  const order: Array<keyof RingfenceInput> = ["title", "fromDate", "toDate", "divisions", "owner", "warehouse"];
  const firstInvalid = order.find((field) => errors[field]);
  if (!firstInvalid) return;
  document.getElementById("ringfence-" + toKebabCase(firstInvalid))?.focus();
}

function toKebabCase(value: string): string {
  return value.replace(/[A-Z]/g, (letter) => "-" + letter.toLocaleLowerCase());
}
