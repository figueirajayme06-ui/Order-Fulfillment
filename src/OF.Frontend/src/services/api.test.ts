import { afterEach, describe, expect, it, vi } from "vitest";

const axiosMock = vi.hoisted(() => {
  const responseUse = vi.fn();
  const instance = {
    interceptors: {
      response: {
        use: responseUse,
      },
    },
  };

  return {
    create: vi.fn(() => instance),
    instance,
    responseUse,
  };
});

vi.mock("axios", () => ({
  default: {
    create: axiosMock.create,
  },
}));

import api from "./api";

type FulfilledHandler = (response: unknown) => unknown;
type RejectedHandler = (error: unknown) => Promise<never>;

function getResponseHandlers(): [FulfilledHandler, RejectedHandler] {
  return axiosMock.responseUse.mock.calls[0] as unknown as [FulfilledHandler, RejectedHandler];
}

describe("api", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("creates a same-origin Axios instance and registers a response interceptor", () => {
    expect(api).toBe(axiosMock.instance);
    expect(axiosMock.create).toHaveBeenCalledWith({});
    expect(axiosMock.responseUse).toHaveBeenCalledOnce();
  });

  it("passes successful responses through unchanged", () => {
    const [onFulfilled] = getResponseHandlers();
    const response = { data: { ok: true } };

    expect(onFulfilled(response)).toBe(response);
  });

  it("reloads the page for a 401 and still rejects the original error", async () => {
    const reload = vi.fn();
    vi.stubGlobal("window", { location: { reload } });
    const [, onRejected] = getResponseHandlers();
    const error = { config: { url: "/api/agreements" }, response: { status: 401 } };

    await expect(onRejected(error)).rejects.toBe(error);

    expect(reload).toHaveBeenCalledOnce();
  });

  it("does not reload when the current-user probe returns 401", async () => {
    const reload = vi.fn();
    vi.stubGlobal("window", { location: { reload } });
    const [, onRejected] = getResponseHandlers();
    const error = { config: { url: "/api/auth/me" }, response: { status: 401 } };

    await expect(onRejected(error)).rejects.toBe(error);

    expect(reload).not.toHaveBeenCalled();
  });

  it("does not reload for other failures and still rejects the original error", async () => {
    const reload = vi.fn();
    vi.stubGlobal("window", { location: { reload } });
    const [, onRejected] = getResponseHandlers();
    const error = { response: { status: 403 } };

    await expect(onRejected(error)).rejects.toBe(error);

    expect(reload).not.toHaveBeenCalled();
  });
});
