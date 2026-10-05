import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import "../../i18n";
import { AuthFailurePage } from "./AuthFailurePage";

describe("AuthFailurePage", () => {
  afterEach(cleanup);

  it("explains how an authenticated but unconfigured user can get access", () => {
    render(<AuthFailurePage reason="notProvisioned" />);

    expect(screen.getByRole("heading", { name: "You don't currently have access to NOF" })).toBeInTheDocument();
    expect(screen.getByText(/signed in with an Aggreko account/i)).toBeInTheDocument();
    expect(screen.getByText(/ask your NOF administrator/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Try again" })).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "Aggreko" })).toBeInTheDocument();
  });

  it.each([
    ["authenticationRequired", "Sign in to continue"],
    ["serviceUnavailable", "NOF couldn't confirm your access"],
  ] as const)("renders the %s state without provisioning guidance", (reason, heading) => {
    render(<AuthFailurePage reason={reason} />);

    expect(screen.getByRole("heading", { name: heading })).toBeInTheDocument();
    expect(screen.queryByText(/ask your NOF administrator/i)).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Try again" })).toBeInTheDocument();
  });
});
