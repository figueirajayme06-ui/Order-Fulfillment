import { ActivationStatus, ApiFulfilmentStatus, type AgreementHeader, type AgreementLine } from "../../types";

export function canActivateAgreement(header: AgreementHeader, lines: readonly AgreementLine[]): boolean {
  return (
    !header.isDeleted &&
    /^[TA]/i.test(header.agreementNumber?.trim() ?? "") &&
    header.fulfilmentStatus === ApiFulfilmentStatus.FullyFulfilled &&
    (header.activationStatus === ActivationStatus.TODO || header.activationStatus === ActivationStatus.Failed) &&
    !lines.some((line) => !line.isDeleted && line.activationStatus === ActivationStatus.Requested)
  );
}

export function canViewOrderSummary(header: AgreementHeader): boolean {
  return (
    !header.isDeleted &&
    /^A/i.test(header.agreementNumber?.trim() ?? "") &&
    header.fulfilmentStatus === ApiFulfilmentStatus.FullyFulfilled
  );
}

export function buildOrderSummaryUrl(
  baseUrl: string | null,
  agreementNumber: string | null,
  quoteId: string | null,
): string | null {
  if (!baseUrl || !agreementNumber?.trim()) return null;
  try {
    const base = new URL(baseUrl);
    if (!["http:", "https:"].includes(base.protocol) || base.username || base.password) return null;
    base.search = "";
    base.hash = "";
    base.pathname = base.pathname.replace(/\/?$/, "/");
    const report = new URL(`report/${encodeURIComponent(agreementNumber.trim())}/display`, base);
    if (quoteId) report.searchParams.set("quoteId", quoteId);
    return report.href;
  } catch {
    return null;
  }
}
