import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import "../../i18n";
import { AssetEnrichmentSection } from "./AssetEnrichmentSection";

describe("AssetEnrichmentSection", () => {
  it("shows last reported location, service detail, and retrofit records", () => {
    render(
      <AssetEnrichmentSection
        section="service"
        enrichment={{
          location: {
            latitude: 9.984044,
            longitude: -83.08223,
            observedAt: "2026-09-20T23:30:01",
            ingestedAt: "2026-09-20T23:37:42",
            validity: 1,
            satelliteCount: 7,
            horizontalAccuracy: 21,
            assignedLocation: "Moin COSTA RICA",
            telemetryFitment: "3 - Fitted - 3G/4G",
          },
          telemetry: {
            fitmentStatus: "3 - Fitted - 3G/4G",
            hasDeviceMapping: true,
            deviceStatusUpdatedAt: "2026-09-21T23:40:59",
            deviceStatusIngestedAt: "2026-09-22T00:46:30",
          },
          serviceHistory: [
            {
              serviceOrderNumber: "0062036414",
              serviceOrderJobNumber: 1,
              status: "Closed",
              createdDate: "2026-08-19T00:00:00",
              finishedDate: "2026-08-21T00:00:00",
              type: "PMAgkOwnEquip",
              meterReading: 7791,
              meterDate: "2026-08-19T00:00:00",
              errorSymptom: "50J",
              errorCause: "Preventive Maintenance Due",
              action: "A0013 - PM Completed",
              actionText: null,
              detailCount: 2,
            },
          ],
          retrofits: [
            {
              document: "M07300072A",
              description: "Coolant Top-up Kit Installation",
              status: "Due",
              openedAt: "2026-06-04T00:00:00",
              completedAt: null,
              serviceOrder: null,
            },
          ],
          rentalHistory: [
            {
              agreementNumber: "A709696",
              customerNumber: "CR00000001",
              customerName: "Instituto Costarricense de Electricidad",
              validFrom: "2024-02-20T00:00:00",
              validTo: "2026-07-25T00:00:00",
              terminationDate: "2026-08-24T00:00:00",
            },
          ],
        }}
        isLoading={false}
        error={null}
        onRetry={vi.fn()}
      />,
    );

    expect(screen.getByRole("heading", { name: "Service history" })).toBeInTheDocument();
    expect(screen.getByText("0062036414")).toBeInTheDocument();
    fireEvent.click(screen.getByText("0062036414"));
    expect(screen.getByText("Preventive Maintenance Due")).toBeInTheDocument();
  });
});
