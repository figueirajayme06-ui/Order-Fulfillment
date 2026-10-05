import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import "../../i18n";
import { useAuth } from "./useAuth";
import { AuthProvider } from "./AuthProvider";

const apiMock = vi.hoisted(() => ({
  get: vi.fn(),
}));

vi.mock("../../services/api", () => ({
  default: apiMock,
}));

describe("AuthProvider", () => {
  afterEach(() => {
    cleanup();
    apiMock.get.mockReset();
  });

  it.each([
    [403, { code: "user_not_provisioned" }, "notProvisioned"],
    [401, undefined, "authenticationRequired"],
    [503, undefined, "serviceUnavailable"],
    [403, { code: "another_problem" }, "serviceUnavailable"],
  ] as const)("classifies a %s current-user response", async (status, data, expected) => {
    apiMock.get.mockRejectedValue({ response: { status, data } });

    render(
      <AuthProvider>
        <AuthState />
      </AuthProvider>,
    );

    expect(await screen.findByTestId("auth-error")).toHaveTextContent(expected);
  });
});

function AuthState() {
  const { error, isLoading } = useAuth();

  if (isLoading) return <span>loading</span>;
  return <span data-testid="auth-error">{error}</span>;
}
