import api from "./api";

export interface UserListItem {
  loginName: string;
  fullName: string;
  division: string;
  isAdmin: boolean;
  isSuperAdmin: boolean;
  language: number;
  dateFormat: string;
  roles: string | null;
  lastLoginAtUtc: string | null;
}

export interface AdminDivisionOption {
  code: string;
  name: string;
}

export interface AdminLanguageOption {
  value: number;
  label: string;
}

export interface AdminOptions {
  divisions: AdminDivisionOption[];
  languages: AdminLanguageOption[];
  dateFormats: string[];
  rolesEnabled: boolean;
  roles: string[];
}

export interface DirectoryPerson {
  loginName: string;
  fullName: string;
  mail: string | null;
}

export interface AdminUserInput {
  fullName: string;
  division: string;
  isAdmin: boolean;
  isSuperAdmin: boolean;
  language: number;
  dateFormat: string;
  roles: string | null;
}

export async function fetchUsers(): Promise<UserListItem[]> {
  const response = await api.get<UserListItem[]>("/api/admin/users");
  return response.data;
}

export async function fetchAdminOptions(): Promise<AdminOptions> {
  const response = await api.get<AdminOptions>("/api/admin/options");
  return response.data;
}

export async function searchDirectoryPeople(search: string): Promise<DirectoryPerson[]> {
  const params = new URLSearchParams({ search });
  const response = await api.get<DirectoryPerson[]>(`/api/admin/people?${params.toString()}`);
  return response.data;
}

export async function createUser(data: AdminUserInput & { loginName: string }): Promise<UserListItem> {
  const response = await api.post<UserListItem>("/api/admin/users", data);
  return response.data;
}

export async function updateUser(loginName: string, data: AdminUserInput): Promise<UserListItem> {
  const response = await api.put<UserListItem>(`/api/admin/users/${encodeURIComponent(loginName)}`, data);
  return response.data;
}

export async function deleteUser(loginName: string): Promise<void> {
  await api.delete(`/api/admin/users/${encodeURIComponent(loginName)}`);
}
