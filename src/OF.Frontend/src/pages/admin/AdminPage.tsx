import axios from "axios";
import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, type FC, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { Alert, Badge, Button, Card, TableSkeleton } from "../../components/common";
import { useAuth } from "../../contexts/auth";
import { readSessionPageState, writeSessionPageState } from "../../lib/sessionPageState";
import {
  createUser,
  deleteUser,
  fetchAdminOptions,
  fetchUsers,
  searchDirectoryPeople,
  updateUser,
  type AdminOptions,
  type AdminUserInput,
  type DirectoryPerson,
  type UserListItem,
} from "../../services/adminService";
import styles from "./AdminPage.module.css";

type GroupBy = "none" | "division" | "language" | "access";

interface AdminPageState {
  stateVersion: 1;
  filter: string;
  groupBy: GroupBy;
}

function parseAdminPageState(value: unknown): AdminPageState | null {
  if (!value || typeof value !== "object") return null;
  const state = value as Partial<AdminPageState>;
  if (
    state.stateVersion !== 1 ||
    typeof state.filter !== "string" ||
    (state.groupBy !== "none" &&
      state.groupBy !== "division" &&
      state.groupBy !== "language" &&
      state.groupBy !== "access")
  ) {
    return null;
  }
  return state as AdminPageState;
}

interface UserDraft {
  loginName: string;
  fullName: string;
  divisions: string[];
  language: number;
  dateFormat: string;
  isAdmin: boolean;
  isSuperAdmin: boolean;
  roles: string[];
}

interface EditorState {
  mode: "create" | "edit";
  draft: UserDraft;
}

interface FeedbackState {
  variant: "success" | "error";
  message: string;
}

const sortUsers = (users: UserListItem[]) =>
  [...users].sort(
    (left, right) =>
      left.fullName.localeCompare(right.fullName, undefined, { sensitivity: "base" }) ||
      left.loginName.localeCompare(right.loginName, undefined, { sensitivity: "base" }),
  );

const splitCsv = (value: string | null | undefined): string[] =>
  value
    ?.split(",")
    .map((item) => item.trim())
    .filter(Boolean) ?? [];

const READ_ONLY_ROLE = "ReadOnly";

export const AdminPage: FC = () => {
  const { t, i18n } = useTranslation();
  const { user } = useAuth();
  const canAccess = Boolean((user?.isAdmin || user?.isSuperAdmin) && !user?.isReadOnly);
  const restoredWorkingState = useMemo(
    () => readSessionPageState(user?.loginName, "admin", parseAdminPageState),
    [user?.loginName],
  );
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [options, setOptions] = useState<AdminOptions | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState("");
  const [feedback, setFeedback] = useState<FeedbackState | null>(null);
  const [editor, setEditor] = useState<EditorState | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [deletingLogin, setDeletingLogin] = useState("");
  const [filter, setFilter] = useState(() => restoredWorkingState?.filter ?? "");
  const [groupBy, setGroupBy] = useState<GroupBy>(() => restoredWorkingState?.groupBy ?? "none");
  const [directoryQuery, setDirectoryQuery] = useState("");
  const [directoryResults, setDirectoryResults] = useState<DirectoryPerson[]>([]);
  const [directoryError, setDirectoryError] = useState("");
  const [hasSearchedDirectory, setHasSearchedDirectory] = useState(false);
  const [isSearchingDirectory, setIsSearchingDirectory] = useState(false);
  const mountedUserIdentifierRef = useRef(user?.loginName);
  const workingStateRef = useRef<AdminPageState>({ stateVersion: 1, filter, groupBy });

  useLayoutEffect(() => {
    workingStateRef.current = { stateVersion: 1, filter, groupBy };
  }, [filter, groupBy]);

  useEffect(
    () => () => {
      writeSessionPageState(mountedUserIdentifierRef.current, "admin", workingStateRef.current);
    },
    [],
  );

  useEffect(() => {
    const timeout = window.setTimeout(
      () => writeSessionPageState(mountedUserIdentifierRef.current, "admin", { stateVersion: 1, filter, groupBy }),
      250,
    );
    return () => window.clearTimeout(timeout);
  }, [filter, groupBy]);

  const loadAdminData = useCallback(async () => {
    if (!canAccess) {
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setLoadError("");
    try {
      const [nextUsers, nextOptions] = await Promise.all([fetchUsers(), fetchAdminOptions()]);
      setUsers(sortUsers(nextUsers));
      setOptions(nextOptions);
    } catch (error) {
      setLoadError(getErrorMessage(error, t("admin.loadError")));
    } finally {
      setIsLoading(false);
    }
  }, [canAccess, t]);

  useEffect(() => {
    void loadAdminData();
  }, [loadAdminData]);

  const languageNames = useMemo(
    () => new Map(options?.languages.map((language) => [language.value, language.label]) ?? []),
    [options],
  );

  const visibleUsers = useMemo(() => {
    const normalizedFilter = filter.trim().toLocaleLowerCase();
    if (!normalizedFilter) return users;

    return users.filter((item) =>
      [
        item.loginName,
        item.fullName,
        item.division,
        languageNames.get(item.language),
        item.dateFormat,
        item.roles,
        accessLabel(item, t),
      ].some((value) => value?.toLocaleLowerCase().includes(normalizedFilter)),
    );
  }, [filter, languageNames, t, users]);

  const groupedUsers = useMemo(() => {
    if (groupBy === "none") return [{ label: "", users: visibleUsers }];

    const groups = new Map<string, UserListItem[]>();
    visibleUsers.forEach((item) => {
      const label =
        groupBy === "division"
          ? item.division || t("admin.notSet")
          : groupBy === "language"
            ? (languageNames.get(item.language) ?? t("admin.notSet"))
            : accessLabel(item, t);
      groups.set(label, [...(groups.get(label) ?? []), item]);
    });

    return [...groups.entries()]
      .sort(([left], [right]) => left.localeCompare(right, undefined, { sensitivity: "base" }))
      .map(([label, grouped]) => ({ label, users: grouped }));
  }, [groupBy, languageNames, t, visibleUsers]);

  const openCreate = () => {
    if (!options) return;
    setFeedback(null);
    resetDirectorySearch();
    setEditor({
      mode: "create",
      draft: {
        loginName: "",
        fullName: "",
        divisions: [],
        language: options.languages[0]?.value ?? 0,
        dateFormat: options.dateFormats[0] ?? "dd/MM/yyyy",
        isAdmin: false,
        isSuperAdmin: false,
        roles: [],
      },
    });
  };

  const openEdit = (item: UserListItem) => {
    const roles = splitCsv(item.roles);
    const isReadOnly = roles.some((role) => role.toLocaleLowerCase() === READ_ONLY_ROLE.toLocaleLowerCase());
    setFeedback(null);
    resetDirectorySearch();
    setEditor({
      mode: "edit",
      draft: {
        loginName: item.loginName,
        fullName: item.fullName,
        divisions: splitCsv(item.division),
        language: item.language,
        dateFormat: item.dateFormat || "dd/MM/yyyy",
        isAdmin: isReadOnly ? false : item.isAdmin,
        isSuperAdmin: isReadOnly ? false : item.isSuperAdmin,
        roles,
      },
    });
  };

  const cancelEdit = () => {
    setEditor(null);
    resetDirectorySearch();
  };

  const resetDirectorySearch = () => {
    setDirectoryQuery("");
    setDirectoryResults([]);
    setDirectoryError("");
    setHasSearchedDirectory(false);
    setIsSearchingDirectory(false);
  };

  const handleDirectorySearch = async () => {
    const search = directoryQuery.trim();
    if (search.length < 2) {
      setDirectoryError(t("admin.directoryMinLength"));
      return;
    }

    setIsSearchingDirectory(true);
    setDirectoryError("");
    setHasSearchedDirectory(true);
    try {
      setDirectoryResults(await searchDirectoryPeople(search));
    } catch (error) {
      setDirectoryResults([]);
      setDirectoryError(getErrorMessage(error, t("admin.directoryError")));
    } finally {
      setIsSearchingDirectory(false);
    }
  };

  const selectDirectoryPerson = (person: DirectoryPerson) => {
    const exists = users.some((item) => item.loginName.toLocaleLowerCase() === person.loginName.toLocaleLowerCase());
    if (exists) {
      setDirectoryError(t("admin.duplicateUser"));
      return;
    }

    setEditor((current) =>
      current
        ? {
            ...current,
            draft: { ...current.draft, loginName: person.loginName, fullName: person.fullName },
          }
        : current,
    );
    setDirectoryResults([]);
    setDirectoryError("");
  };

  const toggleDraftValue = (field: "divisions" | "roles", value: string) => {
    setEditor((current) => {
      if (!current) return current;
      const values = current.draft[field];
      const nextValues = values.includes(value) ? values.filter((item) => item !== value) : [...values, value];
      const selectingReadOnly = field === "roles" && value === READ_ONLY_ROLE && !values.includes(value);
      return {
        ...current,
        draft: {
          ...current.draft,
          [field]: nextValues,
          ...(selectingReadOnly ? { isAdmin: false, isSuperAdmin: false } : {}),
        },
      };
    });
  };

  const setAccessFlag = (field: "isAdmin" | "isSuperAdmin", checked: boolean) => {
    setEditor((current) =>
      current
        ? {
            ...current,
            draft: {
              ...current.draft,
              [field]: checked,
              roles: checked ? current.draft.roles.filter((role) => role !== READ_ONLY_ROLE) : current.draft.roles,
            },
          }
        : current,
    );
  };

  const saveEditor = async (event: FormEvent) => {
    event.preventDefault();
    if (!editor || !isDraftValid(editor.draft)) return;

    const payload: AdminUserInput = {
      fullName: editor.draft.fullName.trim(),
      division: editor.draft.divisions.join(","),
      isAdmin: editor.draft.isAdmin,
      isSuperAdmin: editor.draft.isSuperAdmin,
      language: editor.draft.language,
      dateFormat: editor.draft.dateFormat,
      roles: editor.draft.roles.join(",") || null,
    };

    setIsSaving(true);
    setFeedback(null);
    try {
      const saved =
        editor.mode === "create"
          ? await createUser({ ...payload, loginName: editor.draft.loginName })
          : await updateUser(editor.draft.loginName, payload);
      setUsers((current) => sortUsers([...current.filter((item) => item.loginName !== saved.loginName), saved]));
      setEditor(null);
      resetDirectorySearch();
      setFeedback({
        variant: "success",
        message: t(editor.mode === "create" ? "admin.createSuccess" : "admin.updateSuccess", {
          name: saved.fullName,
        }),
      });
    } catch (error) {
      setFeedback({ variant: "error", message: getErrorMessage(error, t("admin.saveError")) });
    } finally {
      setIsSaving(false);
    }
  };

  const handleDelete = async (item: UserListItem) => {
    if (!window.confirm(t("admin.deleteConfirm", { name: item.fullName, login: item.loginName }))) return;

    setDeletingLogin(item.loginName);
    setFeedback(null);
    try {
      await deleteUser(item.loginName);
      setUsers((current) => current.filter((userItem) => userItem.loginName !== item.loginName));
      if (editor?.draft.loginName === item.loginName) setEditor(null);
      setFeedback({ variant: "success", message: t("admin.deleteSuccess", { name: item.fullName }) });
    } catch (error) {
      setFeedback({ variant: "error", message: getErrorMessage(error, t("admin.deleteError")) });
    } finally {
      setDeletingLogin("");
    }
  };

  if (!canAccess) {
    return <div className={styles.forbidden}>{t("admin.forbidden")}</div>;
  }

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <div>
          <h1>{t("admin.title")}</h1>
          <p>{t("admin.description")}</p>
        </div>
        <span data-print-hidden>
          <Button label={t("admin.addUser")} onClick={openCreate} disabled={isLoading || !options || Boolean(editor)} />
        </span>
      </header>

      {feedback && (
        <Alert variant={feedback.variant} onDismiss={() => setFeedback(null)}>
          {feedback.message}
        </Alert>
      )}

      {loadError && (
        <Alert variant="error">
          <div className={styles.alertContent}>
            <span>{loadError}</span>
            <Button label={t("common.retry")} size="small" variant="secondary" onClick={() => void loadAdminData()} />
          </div>
        </Alert>
      )}

      {editor && options && (
        <Card title={t(editor.mode === "create" ? "admin.newUser" : "admin.editUser")} data-print-hidden>
          <form className={styles.editor} onSubmit={(event) => void saveEditor(event)}>
            {editor.mode === "create" ? (
              <div className={styles.directorySection}>
                <div className={styles.directorySearch}>
                  <label htmlFor="admin-directory-search">{t("admin.directorySearch")}</label>
                  <div className={styles.searchControl}>
                    <input
                      id="admin-directory-search"
                      type="search"
                      value={directoryQuery}
                      onChange={(event) => setDirectoryQuery(event.target.value)}
                      onKeyDown={(event) => {
                        if (event.key === "Enter") {
                          event.preventDefault();
                          void handleDirectorySearch();
                        }
                      }}
                      placeholder={t("admin.directoryPlaceholder")}
                      autoComplete="off"
                    />
                    <Button
                      type="button"
                      label={isSearchingDirectory ? t("admin.searching") : t("common.search")}
                      disabled={isSearchingDirectory}
                      onClick={() => void handleDirectorySearch()}
                    />
                  </div>
                </div>
                {directoryError && <p className={styles.fieldError}>{directoryError}</p>}
                {directoryResults.length > 0 && (
                  <ul className={styles.directoryResults} aria-label={t("admin.directoryResults")}>
                    {directoryResults.map((person) => {
                      const exists = users.some(
                        (item) => item.loginName.toLocaleLowerCase() === person.loginName.toLocaleLowerCase(),
                      );
                      return (
                        <li key={person.loginName}>
                          <button
                            type="button"
                            aria-label={`${person.fullName} — ${person.loginName}${exists ? ` — ${t("admin.alreadyAdded")}` : ""}`}
                            disabled={exists}
                            onClick={() => selectDirectoryPerson(person)}
                          >
                            <span>{person.fullName}</span>
                            <small>{person.loginName}</small>
                            {exists && <strong>{t("admin.alreadyAdded")}</strong>}
                          </button>
                        </li>
                      );
                    })}
                  </ul>
                )}
                {hasSearchedDirectory &&
                  !editor.draft.loginName &&
                  !isSearchingDirectory &&
                  !directoryError &&
                  directoryResults.length === 0 && (
                    <p className={styles.directoryEmpty}>{t("admin.noDirectoryResults")}</p>
                  )}
              </div>
            ) : null}

            <div className={styles.identityGrid}>
              <label>
                {t("admin.loginName")}
                <input value={editor.draft.loginName} readOnly aria-readonly="true" />
              </label>
              <label>
                {t("admin.fullName")}
                <input value={editor.draft.fullName} readOnly aria-readonly="true" />
              </label>
            </div>

            <div className={styles.settingsGrid}>
              <fieldset className={styles.choiceFieldset}>
                <legend>{t("admin.divisions")}</legend>
                <div className={styles.choiceList}>
                  {mergeDivisionOptions(options, editor.draft.divisions).map((division) => (
                    <label key={division.code}>
                      <input
                        type="checkbox"
                        checked={editor.draft.divisions.includes(division.code)}
                        onChange={() => toggleDraftValue("divisions", division.code)}
                      />
                      <span>{division.name ? `${division.code} — ${division.name}` : division.code}</span>
                    </label>
                  ))}
                </div>
                {editor.draft.divisions.length === 0 && (
                  <span className={styles.fieldError}>{t("admin.divisionRequired")}</span>
                )}
              </fieldset>

              <div className={styles.preferenceFields}>
                <label>
                  {t("admin.language")}
                  <select
                    value={editor.draft.language}
                    onChange={(event) =>
                      setEditor((current) =>
                        current
                          ? { ...current, draft: { ...current.draft, language: Number(event.target.value) } }
                          : current,
                      )
                    }
                  >
                    {options.languages.map((language) => (
                      <option key={language.value} value={language.value}>
                        {language.label}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  {t("admin.dateFormat")}
                  <select
                    value={editor.draft.dateFormat}
                    onChange={(event) =>
                      setEditor((current) =>
                        current ? { ...current, draft: { ...current.draft, dateFormat: event.target.value } } : current,
                      )
                    }
                  >
                    {options.dateFormats.map((dateFormat) => (
                      <option key={dateFormat} value={dateFormat}>
                        {dateFormat}
                      </option>
                    ))}
                  </select>
                </label>
              </div>

              <fieldset className={styles.accessFieldset}>
                <legend>{t("admin.access")}</legend>
                <label>
                  <input
                    type="checkbox"
                    checked={editor.draft.isAdmin}
                    onChange={(event) => setAccessFlag("isAdmin", event.target.checked)}
                  />
                  {t("admin.isAdmin")}
                </label>
                {(user?.isSuperAdmin || editor.draft.isSuperAdmin) && (
                  <label>
                    <input
                      type="checkbox"
                      checked={editor.draft.isSuperAdmin}
                      disabled={!user?.isSuperAdmin}
                      onChange={(event) => setAccessFlag("isSuperAdmin", event.target.checked)}
                    />
                    {t("admin.isSuperAdmin")}
                  </label>
                )}
              </fieldset>

              {options.roles.length > 0 && (
                <fieldset className={styles.choiceFieldset}>
                  <legend>{t("admin.featureRoles")}</legend>
                  <div className={styles.choiceList}>
                    {options.roles.map((role) => (
                      <label key={role}>
                        <input
                          type="checkbox"
                          checked={editor.draft.roles.includes(role)}
                          onChange={() => toggleDraftValue("roles", role)}
                        />
                        <span>{role === READ_ONLY_ROLE ? t("admin.readOnly") : role}</span>
                      </label>
                    ))}
                  </div>
                </fieldset>
              )}
            </div>

            <div className={styles.formActions}>
              <Button
                type="submit"
                label={isSaving ? t("common.saving") : t("common.save")}
                disabled={isSaving || !isDraftValid(editor.draft)}
              />
              <Button label={t("common.cancel")} variant="secondary" onClick={cancelEdit} disabled={isSaving} />
            </div>
          </form>
        </Card>
      )}

      <section className={styles.usersSection} aria-labelledby="admin-users-heading">
        <div className={styles.usersToolbar} data-print-hidden>
          <div>
            <h2 id="admin-users-heading">{t("admin.users")}</h2>
            <span>{t("admin.userCount", { count: users.length })}</span>
          </div>
          <div className={styles.tableControls}>
            <label>
              <span>{t("common.search")}</span>
              <input
                type="search"
                value={filter}
                onChange={(event) => setFilter(event.target.value)}
                placeholder={t("admin.filterPlaceholder")}
              />
            </label>
            <label>
              <span>{t("admin.groupBy")}</span>
              <select value={groupBy} onChange={(event) => setGroupBy(event.target.value as GroupBy)}>
                <option value="none">{t("admin.groupNone")}</option>
                <option value="division">{t("admin.divisions")}</option>
                <option value="language">{t("admin.language")}</option>
                <option value="access">{t("admin.access")}</option>
              </select>
            </label>
          </div>
        </div>

        {isLoading ? (
          <TableSkeleton columns={9} rows={8} ariaLabel={t("admin.loadingUsers")} />
        ) : !loadError && users.length === 0 ? (
          <div className={styles.emptyState}>
            <h3>{t("admin.noUsers")}</h3>
            <p>{t("admin.noUsersDescription")}</p>
          </div>
        ) : !loadError && visibleUsers.length === 0 ? (
          <div className={styles.emptyState}>
            <h3>{t("admin.noMatchingUsers")}</h3>
            <Button label={t("admin.clearSearch")} variant="secondary" onClick={() => setFilter("")} />
          </div>
        ) : (
          <div className={styles.tableContainer} data-print-table="standard">
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>{t("admin.loginName")}</th>
                  <th>{t("admin.fullName")}</th>
                  <th>{t("admin.divisions")}</th>
                  <th>{t("admin.language")}</th>
                  <th>{t("admin.dateFormat")}</th>
                  <th>{t("admin.featureRoles")}</th>
                  <th>{t("admin.lastNofAccess")}</th>
                  <th>{t("admin.access")}</th>
                  <th data-print-hidden>{t("common.actions")}</th>
                </tr>
              </thead>
              {groupedUsers.map((group) => (
                <tbody key={group.label || "all"}>
                  {group.label && (
                    <tr className={styles.groupRow}>
                      <th colSpan={9}>{t("admin.groupHeading", { group: group.label, count: group.users.length })}</th>
                    </tr>
                  )}
                  {group.users.map((item) => (
                    <tr key={item.loginName}>
                      <td className={styles.loginCell}>{item.loginName}</td>
                      <td>{item.fullName}</td>
                      <td>{item.division || "—"}</td>
                      <td>{languageNames.get(item.language) ?? item.language}</td>
                      <td>{item.dateFormat || "—"}</td>
                      <td>{item.roles || "—"}</td>
                      <td>{renderLastNofAccess(item.lastLoginAtUtc, i18n.resolvedLanguage, t)}</td>
                      <td>
                        <div className={styles.badges}>
                          {hasRole(item, READ_ONLY_ROLE) ? (
                            <Badge label={t("admin.readOnly")} variant="neutral" />
                          ) : (
                            <>
                              {item.isSuperAdmin && <Badge label={t("admin.isSuperAdmin")} variant="error" />}
                              {item.isAdmin && <Badge label={t("admin.isAdmin")} variant="warning" />}
                            </>
                          )}
                          {!hasRole(item, READ_ONLY_ROLE) && !item.isAdmin && !item.isSuperAdmin && (
                            <Badge label={t("admin.standardUser")} variant="neutral" />
                          )}
                        </div>
                      </td>
                      <td data-print-hidden>
                        <div className={styles.rowActions}>
                          <Button
                            label={t("common.edit")}
                            variant="secondary"
                            size="small"
                            onClick={() => openEdit(item)}
                            disabled={Boolean(editor) || Boolean(deletingLogin)}
                          />
                          {user?.isSuperAdmin && (
                            <Button
                              label={deletingLogin === item.loginName ? t("common.deleting") : t("common.delete")}
                              variant="danger"
                              size="small"
                              onClick={() => void handleDelete(item)}
                              disabled={Boolean(editor) || Boolean(deletingLogin)}
                            />
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              ))}
            </table>
          </div>
        )}
      </section>
    </div>
  );
};

function isDraftValid(draft: UserDraft): boolean {
  return Boolean(draft.loginName.trim() && draft.fullName.trim() && draft.divisions.length > 0);
}

function mergeDivisionOptions(options: AdminOptions, selectedDivisions: string[]) {
  const knownCodes = new Set(options.divisions.map((division) => division.code));
  return [
    ...options.divisions,
    ...selectedDivisions.filter((code) => !knownCodes.has(code)).map((code) => ({ code, name: "" })),
  ];
}

function accessLabel(item: UserListItem, t: (key: string) => string): string {
  if (hasRole(item, READ_ONLY_ROLE)) return t("admin.readOnly");
  if (item.isSuperAdmin) return t("admin.isSuperAdmin");
  if (item.isAdmin) return t("admin.isAdmin");
  return t("admin.standardUser");
}

function hasRole(item: UserListItem, role: string): boolean {
  return splitCsv(item.roles).some((value) => value.toLocaleLowerCase() === role.toLocaleLowerCase());
}

function renderLastNofAccess(value: string | null, locale: string | undefined, t: (key: string) => string) {
  if (!value) return t("admin.never");

  const accessedAt = new Date(value);
  if (Number.isNaN(accessedAt.getTime())) return t("admin.never");

  const compact = new Intl.DateTimeFormat(locale, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(accessedAt);
  const full = new Intl.DateTimeFormat(locale, {
    dateStyle: "full",
    timeStyle: "long",
  }).format(accessedAt);

  return (
    <time dateTime={value} title={full} aria-label={`${t("admin.lastNofAccess")}: ${full}`}>
      {compact}
    </time>
  );
}

function getErrorMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError<{ title?: string; detail?: string; message?: string }>(error)) {
    return error.response?.data?.detail ?? error.response?.data?.message ?? error.response?.data?.title ?? fallback;
  }
  return fallback;
}
