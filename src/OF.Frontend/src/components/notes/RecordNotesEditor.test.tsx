import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import "../../i18n";
import { RecordNotesEditor } from "./RecordNotesEditor";

describe("RecordNotesEditor", () => {
  const loadNotes = vi.fn();
  const createNote = vi.fn();
  const saveNote = vi.fn();

  beforeEach(() => {
    vi.resetAllMocks();
    loadNotes.mockResolvedValue([]);
  });

  afterEach(cleanup);

  it("shows an empty state and creates a note", async () => {
    const user = userEvent.setup();
    createNote.mockResolvedValue({
      id: 7,
      notes: "Call before collection",
      lastUpdatedBy: "planner@example.com",
      lastUpdatedDate: null,
    });

    render(
      <RecordNotesEditor recordKey="asset-A-1" loadNotes={loadNotes} createNote={createNote} saveNote={saveNote} />,
    );

    expect(screen.queryByRole("dialog", { name: "Notes" })).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Notes" }));
    expect(
      await screen.findByText("No notes have been recorded yet. Add a note for other planners."),
    ).toBeInTheDocument();
    const input = screen.getByRole("textbox", { name: "Add a note" });
    expect(screen.getByRole("button", { name: "Add note" })).toBeDisabled();

    await user.type(input, "Call before collection");
    expect(screen.getByText("Unsaved changes")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Add note" }));

    expect(createNote).toHaveBeenCalledWith("Call before collection");
    expect(await screen.findByText("Note added.")).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Note 1" })).toHaveValue("Call before collection");
  });

  it("keeps an edited note and presents a retryable error when save fails", async () => {
    const user = userEvent.setup();
    loadNotes.mockResolvedValue([
      { id: 12, notes: "Original", lastUpdatedBy: "planner@example.com", lastUpdatedDate: null },
    ]);
    saveNote.mockRejectedValueOnce(new Error("network"));
    saveNote.mockResolvedValueOnce({ id: 12, notes: "Retained text", lastUpdatedBy: null, lastUpdatedDate: null });

    render(
      <RecordNotesEditor recordKey="agreement-42" loadNotes={loadNotes} createNote={createNote} saveNote={saveNote} />,
    );
    await user.click(screen.getByRole("button", { name: "Notes" }));
    const input = await screen.findByRole("textbox", { name: "Note 1" });
    expect(screen.getByRole("button", { name: "1 note" })).toHaveTextContent("Notes (1)");
    await user.clear(input);
    await user.type(input, "Retained text");
    await user.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("The note could not be saved");
    expect(input).toHaveValue("Retained text");
    expect(screen.getByRole("button", { name: "Save" })).toBeEnabled();

    await user.click(screen.getByRole("button", { name: "Save" }));
    await waitFor(() => expect(saveNote).toHaveBeenCalledTimes(2));
    expect(await screen.findByText("Notes saved.")).toBeInTheDocument();
  });

  it("offers a retry when loading notes fails", async () => {
    const user = userEvent.setup();
    loadNotes.mockRejectedValueOnce(new Error("network")).mockResolvedValueOnce([]);

    render(
      <RecordNotesEditor recordKey="asset-A-1" loadNotes={loadNotes} createNote={createNote} saveNote={saveNote} />,
    );

    await user.click(screen.getByRole("button", { name: "Notes" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Notes could not be loaded");
    await user.click(screen.getByRole("button", { name: "Try again" }));
    expect(await screen.findByRole("textbox", { name: "Add a note" })).toBeEnabled();
    expect(loadNotes).toHaveBeenCalledTimes(2);
  });

  it("keeps an unsaved draft when the dialog is closed and restores focus to the Notes button", async () => {
    const user = userEvent.setup();

    render(
      <RecordNotesEditor recordKey="asset-A-1" loadNotes={loadNotes} createNote={createNote} saveNote={saveNote} />,
    );

    const notesButton = screen.getByRole("button", { name: "Notes" });
    await user.click(notesButton);
    const input = await screen.findByRole("textbox", { name: "Add a note" });
    await user.type(input, "Keep this draft");

    await user.keyboard("{Escape}");
    expect(screen.queryByRole("dialog", { name: "Notes" })).not.toBeInTheDocument();
    expect(notesButton).toHaveFocus();

    await user.click(notesButton);
    expect(await screen.findByRole("textbox", { name: "Add a note" })).toHaveValue("Keep this draft");
  });
});
