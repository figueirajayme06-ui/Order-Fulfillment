export interface RingfenceAssetsContext {
  ringfenceId: number | null;
  returnTo: string | null;
}

/**
 * Reads the optional Ringfence-to-Assets handoff from the URL.
 *
 * `returnTo` is deliberately limited to the Ringfence route so this page
 * cannot be used as an open redirect.
 */
export function parseRingfenceAssetsContext(search: string): RingfenceAssetsContext {
  const query = new URLSearchParams(search);
  const ringfenceIdValue = query.get("ringfenceId");
  const ringfenceId =
    ringfenceIdValue && /^[1-9]\d*$/.test(ringfenceIdValue) && Number.isSafeInteger(Number(ringfenceIdValue))
      ? Number(ringfenceIdValue)
      : null;

  const returnToValue = query.get("returnTo");
  let returnTo: string | null = null;
  if (
    ringfenceId !== null &&
    returnToValue?.startsWith("/") &&
    !returnToValue.startsWith("//") &&
    !returnToValue.includes("\\")
  ) {
    const url = new URL(returnToValue, "https://order-fulfillment.local");
    if (url.pathname === "/ringfence") returnTo = `${url.pathname}${url.search}${url.hash}`;
  }

  return {
    ringfenceId,
    returnTo,
  };
}
