import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { Button } from "./Button";

describe("Button", () => {
  afterEach(cleanup);

  it("supports a contextual accessible name without changing its visible label", () => {
    render(<Button label="Add equipment" ariaLabel="Add equipment to line A-10042-1" />);

    expect(screen.getByRole("button", { name: "Add equipment to line A-10042-1" })).toHaveTextContent("Add equipment");
  });
});
