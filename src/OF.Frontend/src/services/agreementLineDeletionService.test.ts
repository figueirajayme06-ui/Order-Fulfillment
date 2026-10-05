import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "./api";
import { agreementLineDeletionService } from "./agreementLineDeletionService";

vi.mock("./api", () => ({ default: { delete: vi.fn() } }));

describe("agreementLineDeletionService", () => {
  beforeEach(() => vi.clearAllMocks());

  it("deletes the identified line within its agreement and returns mutation counts", async () => {
    const data = { lineId: 11, removedReservationCount: 4, headerStatus: 0 };
    vi.mocked(api.delete).mockResolvedValue({ data });
    await expect(agreementLineDeletionService.deleteLine(42, 11)).resolves.toEqual(data);
    expect(api.delete).toHaveBeenCalledWith("/api/agreements/42/lines/11");
  });

  it("propagates server safeguards without reporting success", async () => {
    const failure = new Error("Last line cannot be deleted");
    vi.mocked(api.delete).mockRejectedValue(failure);
    await expect(agreementLineDeletionService.deleteLine(42, 11)).rejects.toBe(failure);
  });
});
