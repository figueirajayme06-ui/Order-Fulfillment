import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { afterEach, describe, expect, it } from "vitest";
import { applyMultiValueTextFilterDrafts } from "./multiValueTextFilterModel";
import { TableColumnFilter } from "./TableColumnControls";

afterEach(cleanup);

function MultiValueHarness() {
  const [values, setValues] = useState<string[]>([]);
  return <TableColumnFilter multiValue label="Asset ID" value={values} onChange={setValues} />;
}

function LiveMultiValueHarness() {
  const [values, setValues] = useState<string[]>([]);
  const [draft, setDraft] = useState("");
  const effective = applyMultiValueTextFilterDrafts({ id: values }, { id: draft }).id ?? [];
  return (
    <>
      <TableColumnFilter
        multiValue
        label="Asset ID"
        value={values}
        draftValue={draft}
        onChange={setValues}
        onDraftChange={setDraft}
      />
      <output data-testid="effective-filter">{Array.isArray(effective) ? effective.join("|") : effective}</output>
    </>
  );
}

describe("TableColumnFilter multi-value mode", () => {
  it("commits separated values with Enter as removable tokens and clears them", () => {
    render(<MultiValueHarness />);
    const input = screen.getByRole("searchbox", { name: "Filter Asset ID" });
    fireEvent.change(input, { target: { value: "ASSET-101, ASSET-204" } });
    expect(screen.queryByText("ASSET-101")).not.toBeInTheDocument();
    fireEvent.keyDown(input, { key: "Enter" });
    expect(screen.getByText("ASSET-101")).toBeInTheDocument();
    expect(screen.getByText("ASSET-204")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Remove ASSET-101 from Asset ID filter" }));
    expect(screen.queryByText("ASSET-101")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Clear all Asset ID filter values" }));
    expect(screen.queryByText("ASSET-204")).not.toBeInTheDocument();
  });

  it("filters with the active draft before Enter and ORs it with committed tokens", () => {
    render(<LiveMultiValueHarness />);
    const input = screen.getByRole("searchbox", { name: "Filter Asset ID" });

    fireEvent.change(input, { target: { value: "ZAD" } });
    expect(screen.getByTestId("effective-filter")).toHaveTextContent("ZAD");
    expect(screen.queryByRole("button", { name: "Remove ZAD from Asset ID filter" })).not.toBeInTheDocument();

    fireEvent.keyDown(input, { key: "Enter" });
    expect(screen.getByRole("button", { name: "Remove ZAD from Asset ID filter" })).toBeInTheDocument();

    fireEvent.change(input, { target: { value: "YCK" } });
    expect(screen.getByTestId("effective-filter")).toHaveTextContent("ZAD|YCK");
    expect(screen.queryByRole("button", { name: "Remove YCK from Asset ID filter" })).not.toBeInTheDocument();

    fireEvent.keyDown(input, { key: "Enter" });
    expect(screen.getByRole("button", { name: "Remove YCK from Asset ID filter" })).toBeInTheDocument();
  });

  it("commits with Enter and removes the last value with Backspace", () => {
    render(<MultiValueHarness />);
    const input = screen.getByRole("searchbox", { name: "Filter Asset ID" });
    fireEvent.change(input, { target: { value: "ASSET-101" } });
    fireEvent.keyDown(input, { key: "Enter" });
    expect(screen.getByText("ASSET-101")).toBeInTheDocument();
    fireEvent.keyDown(input, { key: "Backspace" });
    expect(screen.queryByText("ASSET-101")).not.toBeInTheDocument();
  });
});
