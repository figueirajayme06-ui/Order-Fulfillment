import { useCallback, useEffect, useId, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { BsExclamationSquare } from "react-icons/bs";
import { Alert, Button, Spinner } from "../common";
import type { RecordNote } from "../../services/notesService";
import styles from "./RecordNotesEditor.module.css";

interface EditableNote extends RecordNote {
  draft: string;
  error: string | null;
  saved: boolean;
}

interface RecordNotesEditorProps {
  recordKey: string;
  loadNotes: () => Promise<RecordNote[]>;
  createNote: (notes: string) => Promise<RecordNote>;
  saveNote: (noteId: number, notes: string) => Promise<RecordNote>;
}

const FOCUSABLE_SELECTOR = [
  "button:not([disabled])",
  "[href]",
  "input:not([disabled]):not([type='hidden'])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  "[tabindex]:not([tabindex='-1'])",
].join(",");

function isVisibleFocusable(element: HTMLElement): boolean {
  const rect = element.getBoundingClientRect();
  return rect.width > 0 && rect.height > 0;
}

export function RecordNotesEditor({ recordKey, loadNotes, createNote, saveNote }: RecordNotesEditorProps) {
  const { t } = useTranslation();
  const sectionId = useId();
  const modalRef = useRef<HTMLDivElement>(null);
  const previousFocusedElementRef = useRef<HTMLElement | null>(null);
  const [notes, setNotes] = useState<EditableNote[]>([]);
  const [newDraft, setNewDraft] = useState("");
  const [isOpen, setIsOpen] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [loadAttempt, setLoadAttempt] = useState(0);
  const [savingNoteId, setSavingNoteId] = useState<number | "new" | null>(null);
  const [newNoteError, setNewNoteError] = useState(false);
  const [newNoteSaved, setNewNoteSaved] = useState(false);
  const isBusy = savingNoteId !== null;
  const hasKnownNotes = !isLoading && !loadError && notes.length > 0;
  const isBusyRef = useRef(isBusy);
  isBusyRef.current = isBusy;

  const getFocusableElements = useCallback(() => {
    if (!modalRef.current) return [] as HTMLElement[];

    return Array.from(modalRef.current.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)).filter(
      (element) => !element.hasAttribute("disabled") && isVisibleFocusable(element),
    );
  }, []);

  const requestClose = useCallback(() => {
    if (!isBusyRef.current) setIsOpen(false);
  }, []);

  useEffect(() => {
    if (!isOpen) return;

    previousFocusedElementRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const rafId = window.requestAnimationFrame(() => {
      const focusables = getFocusableElements();
      if (focusables.length > 0) focusables[0].focus();
      else modalRef.current?.focus();
    });

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        requestClose();
        return;
      }

      if (event.key !== "Tab") return;
      const focusables = getFocusableElements();
      if (focusables.length === 0) {
        event.preventDefault();
        modalRef.current?.focus();
        return;
      }

      const first = focusables[0];
      const last = focusables[focusables.length - 1];
      const active = document.activeElement;
      if (event.shiftKey) {
        if (active === first || !(active instanceof Node) || !modalRef.current?.contains(active)) {
          event.preventDefault();
          last.focus();
        }
      } else if (active === last) {
        event.preventDefault();
        first.focus();
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => {
      window.cancelAnimationFrame(rafId);
      document.removeEventListener("keydown", handleKeyDown);
      const restoreTarget = previousFocusedElementRef.current;
      if (restoreTarget && document.contains(restoreTarget)) restoreTarget.focus();
    };
  }, [getFocusableElements, isOpen, requestClose]);

  useEffect(() => {
    let isCurrent = true;
    setIsLoading(true);
    setLoadError(false);
    setNewDraft("");
    setNewNoteError(false);
    setNewNoteSaved(false);

    void loadNotes()
      .then((result) => {
        if (!isCurrent) return;
        setNotes(result.map(toEditableNote));
      })
      .catch(() => {
        if (isCurrent) setLoadError(true);
      })
      .finally(() => {
        if (isCurrent) setIsLoading(false);
      });

    return () => {
      isCurrent = false;
    };
  }, [loadAttempt, loadNotes, recordKey]);

  const updateDraft = (noteId: number, draft: string) => {
    setNotes((current) =>
      current.map((note) => (note.id === noteId ? { ...note, draft, error: null, saved: false } : note)),
    );
  };

  const handleSave = async (note: EditableNote) => {
    if (note.draft === note.notes || isBusy) return;

    setSavingNoteId(note.id);
    setNotes((current) => current.map((item) => (item.id === note.id ? { ...item, error: null, saved: false } : item)));
    try {
      const saved = await saveNote(note.id, note.draft);
      setNotes((current) =>
        current.map((item) => (item.id === note.id ? { ...toEditableNote(saved), saved: true } : item)),
      );
    } catch {
      setNotes((current) =>
        current.map((item) => (item.id === note.id ? { ...item, error: t("notes.saveError") } : item)),
      );
    } finally {
      setSavingNoteId(null);
    }
  };

  const handleCreate = async () => {
    if (!newDraft.trim() || isBusy) return;

    setSavingNoteId("new");
    setNewNoteError(false);
    setNewNoteSaved(false);
    try {
      const saved = await createNote(newDraft);
      setNotes((current) => [toEditableNote(saved), ...current]);
      setNewDraft("");
      setNewNoteSaved(true);
    } catch {
      setNewNoteError(true);
    } finally {
      setSavingNoteId(null);
    }
  };

  return (
    <>
      <div className={styles.trigger} data-print-hidden>
        <Button
          label={hasKnownNotes ? t("notes.titleWithCount", { count: notes.length }) : t("notes.title")}
          ariaLabel={hasKnownNotes ? t("notes.count", { count: notes.length }) : t("notes.title")}
          icon={hasKnownNotes ? <BsExclamationSquare aria-hidden="true" /> : undefined}
          variant="secondary"
          onClick={() => setIsOpen(true)}
        />
      </div>

      {isOpen && (
        <div className={styles.overlay} onClick={requestClose} role="presentation" data-print-hidden>
          <div
            className={styles.modal}
            onClick={(event) => event.stopPropagation()}
            role="dialog"
            aria-modal="true"
            aria-labelledby={`${sectionId}-title`}
            aria-busy={isBusy}
            tabIndex={-1}
            ref={modalRef}
          >
            <header className={styles.dialogHeader}>
              <h2 id={`${sectionId}-title`}>{t("notes.title")}</h2>
              <button
                type="button"
                className={styles.close}
                onClick={requestClose}
                aria-label={t("notes.closeDialog")}
                disabled={isBusy}
              >
                ×
              </button>
            </header>

            <div className={styles.dialogBody}>
              {isLoading && (
                <div className={styles.status} role="status" aria-live="polite">
                  <Spinner /> {t("notes.loading")}
                </div>
              )}

              {!isLoading && loadError && (
                <div className={styles.loadError}>
                  <Alert variant="error">{t("notes.loadError")}</Alert>
                  <Button
                    label={t("notes.retryLoad")}
                    variant="secondary"
                    onClick={() => setLoadAttempt((attempt) => attempt + 1)}
                  />
                </div>
              )}

              {!isLoading && !loadError && (
                <>
                  {notes.length === 0 && <p className={styles.empty}>{t("notes.empty")}</p>}
                  <div className={styles.noteList}>
                    {notes.map((note, index) => {
                      const isDirty = note.draft !== note.notes;
                      const isSaving = savingNoteId === note.id;
                      return (
                        <article className={styles.note} key={note.id}>
                          <div className={styles.noteHeader}>
                            <label htmlFor={`${sectionId}-note-${note.id}`}>
                              {t("notes.noteLabel", { number: index + 1 })}
                            </label>
                            {isDirty && <span className={styles.dirty}>{t("notes.unsavedChanges")}</span>}
                          </div>
                          {(note.lastUpdatedBy || note.lastUpdatedDate) && (
                            <p className={styles.metadata}>{formatMetadata(note, t("notes.unknownAuthor"))}</p>
                          )}
                          <textarea
                            id={`${sectionId}-note-${note.id}`}
                            className={styles.textarea}
                            value={note.draft}
                            disabled={isBusy}
                            maxLength={10_000}
                            onChange={(event) => updateDraft(note.id, event.target.value)}
                          />
                          {note.error && <Alert variant="error">{note.error}</Alert>}
                          {note.saved && !isDirty && (
                            <p className={styles.status} role="status" aria-live="polite">
                              {t("notes.saved")}
                            </p>
                          )}
                          <div className={styles.actions}>
                            <Button
                              label={isSaving ? t("common.saving") : t("common.save")}
                              onClick={() => void handleSave(note)}
                              disabled={isBusy || !isDirty}
                            />
                          </div>
                        </article>
                      );
                    })}
                  </div>

                  <div className={styles.newNote}>
                    <div className={styles.noteHeader}>
                      <label htmlFor={`${sectionId}-new-note`}>{t("notes.addLabel")}</label>
                      {newDraft && <span className={styles.dirty}>{t("notes.unsavedChanges")}</span>}
                    </div>
                    <textarea
                      id={`${sectionId}-new-note`}
                      className={styles.textarea}
                      value={newDraft}
                      disabled={isBusy}
                      maxLength={10_000}
                      onChange={(event) => {
                        setNewDraft(event.target.value);
                        setNewNoteError(false);
                        setNewNoteSaved(false);
                      }}
                    />
                    {newNoteError && <Alert variant="error">{t("notes.saveError")}</Alert>}
                    {newNoteSaved && (
                      <p className={styles.status} role="status" aria-live="polite">
                        {t("notes.created")}
                      </p>
                    )}
                    <div className={styles.actions}>
                      <Button
                        label={savingNoteId === "new" ? t("notes.adding") : t("notes.add")}
                        onClick={() => void handleCreate()}
                        disabled={isBusy || !newDraft.trim()}
                      />
                    </div>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      )}
    </>
  );
}

function toEditableNote(note: RecordNote): EditableNote {
  return { ...note, notes: note.notes ?? "", draft: note.notes ?? "", error: null, saved: false };
}

function formatMetadata(note: RecordNote, unknownAuthor: string): string {
  const author = note.lastUpdatedBy || unknownAuthor;
  if (!note.lastUpdatedDate) return author;
  return `${author} · ${new Date(note.lastUpdatedDate).toLocaleString()}`;
}
