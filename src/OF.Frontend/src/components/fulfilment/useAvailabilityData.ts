import { useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { fetchAvailabilitySummary } from "../../services/availabilityService";
import {
  fetchDivisions,
  fetchWarehouses,
  type DivisionLookup,
  type WarehouseLookup,
} from "../../services/lookupsService";
import type { AvailabilityItem } from "../../types/availability";
import type { AvailabilityPanelProps } from "./availabilityTypes";

interface AvailabilityDataOptions extends Pick<
  AvailabilityPanelProps,
  "genericCode" | "itemNumber" | "attributes" | "startDate" | "endDate" | "lineId"
> {
  selectedDivisionQuery: string;
}

export function useAvailabilityData({
  genericCode,
  itemNumber,
  attributes,
  startDate,
  endDate,
  lineId,
  selectedDivisionQuery,
}: AvailabilityDataOptions) {
  const { t } = useTranslation();
  const [data, setData] = useState<AvailabilityItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [divisionOptions, setDivisionOptions] = useState<DivisionLookup[]>([]);
  const [divisionOptionsLoading, setDivisionOptionsLoading] = useState(true);
  const [divisionOptionsError, setDivisionOptionsError] = useState(false);
  const [warehouseCatalog, setWarehouseCatalog] = useState<WarehouseLookup[]>([]);
  const [warehouseCatalogError, setWarehouseCatalogError] = useState(false);
  const availabilityRequestId = useRef(0);
  const warehouseCatalogRequestId = useRef(0);
  useEffect(() => {
    let active = true;
    setDivisionOptionsLoading(true);
    setDivisionOptionsError(false);

    fetchDivisions()
      .then((results) => {
        if (active) {
          setDivisionOptions(
            results.map((option) => ({
              ...option,
              code: option.code.trim().toUpperCase(),
            })),
          );
        }
      })
      .catch(() => {
        if (active) setDivisionOptionsError(true);
      })
      .finally(() => {
        if (active) setDivisionOptionsLoading(false);
      });

    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    const requestId = ++warehouseCatalogRequestId.current;
    let active = true;

    setWarehouseCatalog([]);
    setWarehouseCatalogError(false);
    if (!selectedDivisionQuery) {
      return () => {
        active = false;
      };
    }

    fetchWarehouses(selectedDivisionQuery)
      .then((results) => {
        if (active && requestId === warehouseCatalogRequestId.current) {
          setWarehouseCatalog(results);
        }
      })
      .catch(() => {
        if (active && requestId === warehouseCatalogRequestId.current) {
          setWarehouseCatalog([]);
          setWarehouseCatalogError(true);
        }
      });

    return () => {
      active = false;
    };
  }, [selectedDivisionQuery]);

  const loadData = useCallback(async () => {
    const requestId = ++availabilityRequestId.current;
    if (!genericCode || !selectedDivisionQuery) {
      setData([]);
      setLoading(false);
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const results = await fetchAvailabilitySummary(
        genericCode,
        attributes,
        startDate,
        endDate,
        selectedDivisionQuery,
        itemNumber,
        lineId,
      );
      if (requestId === availabilityRequestId.current) setData(results);
    } catch {
      if (requestId === availabilityRequestId.current) setError(t("availability.error"));
    } finally {
      if (requestId === availabilityRequestId.current) setLoading(false);
    }
  }, [genericCode, itemNumber, attributes, startDate, endDate, selectedDivisionQuery, lineId, t]);

  useEffect(() => {
    loadData();
    return () => {
      availabilityRequestId.current += 1;
    };
  }, [loadData]);

  return {
    data,
    loading,
    error,
    divisionOptions,
    divisionOptionsLoading,
    divisionOptionsError,
    warehouseCatalog,
    warehouseCatalogError,
    loadData,
  };
}
