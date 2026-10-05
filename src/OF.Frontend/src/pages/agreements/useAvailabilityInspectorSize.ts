import { useRef, useState, type KeyboardEvent, type PointerEvent } from "react";

const AVAILABILITY_INSPECTOR_SIZE_STORAGE_KEY = "agreementDetail.linesPaneWidth.v2";
const AVAILABILITY_INSPECTOR_DEFAULT_SIZE = 480;
export const AVAILABILITY_INSPECTOR_MIN_SIZE = 440;
export const AVAILABILITY_INSPECTOR_MAX_SIZE = 560;

interface AvailabilityResizeState {
  pointerId: number;
  startX: number;
  startSize: number;
}

export function useAvailabilityInspectorSize() {
  const [availabilityInspectorSize, setAvailabilityInspectorSize] = useState(readAvailabilityInspectorSize);
  const [isResizingAvailability, setIsResizingAvailability] = useState(false);
  const availabilityInspectorSizeRef = useRef(availabilityInspectorSize);
  const availabilityResizeStateRef = useRef<AvailabilityResizeState | null>(null);
  const fulfilmentWorkspaceRef = useRef<HTMLDivElement>(null);
  const linesPaneRef = useRef<HTMLDivElement>(null);
  const updateAvailabilityInspectorSize = (size: number, commit = true) => {
    const nextSize = Math.round(clamp(size, AVAILABILITY_INSPECTOR_MIN_SIZE, AVAILABILITY_INSPECTOR_MAX_SIZE));
    availabilityInspectorSizeRef.current = nextSize;
    if (linesPaneRef.current) {
      linesPaneRef.current.style.flexBasis = `${nextSize}px`;
    }
    if (commit) {
      setAvailabilityInspectorSize(nextSize);
    }
  };

  const handleAvailabilityResizeStart = (event: PointerEvent<HTMLDivElement>) => {
    if (event.button !== 0) return;

    const workspaceWidth = fulfilmentWorkspaceRef.current?.clientWidth ?? 0;
    if (workspaceWidth <= 0) return;

    event.preventDefault();
    event.currentTarget.setPointerCapture(event.pointerId);
    availabilityResizeStateRef.current = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startSize: availabilityInspectorSizeRef.current,
    };
    setIsResizingAvailability(true);
  };

  const handleAvailabilityResizeMove = (event: PointerEvent<HTMLDivElement>) => {
    const resizeState = availabilityResizeStateRef.current;
    if (!resizeState || resizeState.pointerId !== event.pointerId) return;

    updateAvailabilityInspectorSize(resizeState.startSize + event.clientX - resizeState.startX, false);
  };

  const handleAvailabilityResizeEnd = (event: PointerEvent<HTMLDivElement>) => {
    const resizeState = availabilityResizeStateRef.current;
    if (!resizeState || resizeState.pointerId !== event.pointerId) return;

    availabilityResizeStateRef.current = null;
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
    setAvailabilityInspectorSize(availabilityInspectorSizeRef.current);
    persistAvailabilityInspectorSize(availabilityInspectorSizeRef.current);
    setIsResizingAvailability(false);
  };

  const handleAvailabilityResizeKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    let nextSize: number | null = null;
    if (event.key === "ArrowLeft") {
      nextSize = availabilityInspectorSizeRef.current - (event.shiftKey ? 40 : 10);
    } else if (event.key === "ArrowRight") {
      nextSize = availabilityInspectorSizeRef.current + (event.shiftKey ? 40 : 10);
    } else if (event.key === "Home") {
      nextSize = AVAILABILITY_INSPECTOR_MIN_SIZE;
    } else if (event.key === "End") {
      nextSize = AVAILABILITY_INSPECTOR_MAX_SIZE;
    }

    if (nextSize === null) return;
    event.preventDefault();
    updateAvailabilityInspectorSize(nextSize);
    persistAvailabilityInspectorSize(availabilityInspectorSizeRef.current);
  };

  return {
    availabilityInspectorSize,
    isResizingAvailability,
    fulfilmentWorkspaceRef,
    linesPaneRef,
    handleAvailabilityResizeStart,
    handleAvailabilityResizeMove,
    handleAvailabilityResizeEnd,
    handleAvailabilityResizeKeyDown,
  };
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

function readAvailabilityInspectorSize(): number {
  if (typeof window === "undefined") return AVAILABILITY_INSPECTOR_DEFAULT_SIZE;

  try {
    const persistedValue = window.localStorage.getItem(AVAILABILITY_INSPECTOR_SIZE_STORAGE_KEY);
    const persistedSize = persistedValue === null ? Number.NaN : Number(persistedValue);
    if (Number.isFinite(persistedSize)) {
      return Math.round(clamp(persistedSize, AVAILABILITY_INSPECTOR_MIN_SIZE, AVAILABILITY_INSPECTOR_MAX_SIZE));
    }
  } catch {
    // Keep the default when browser storage is unavailable.
  }

  return AVAILABILITY_INSPECTOR_DEFAULT_SIZE;
}

function persistAvailabilityInspectorSize(size: number): void {
  if (typeof window === "undefined") return;

  try {
    window.localStorage.setItem(AVAILABILITY_INSPECTOR_SIZE_STORAGE_KEY, String(size));
  } catch {
    // Resizing remains available when browser storage is unavailable.
  }
}
