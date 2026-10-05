import { createContext } from "react";
import type { UserInfo } from "../../types";

export type AuthFailureReason = "notProvisioned" | "authenticationRequired" | "serviceUnavailable";

export interface AuthContextValue {
  user: UserInfo | null;
  isLoading: boolean;
  error: AuthFailureReason | null;
}

export const AuthContext = createContext<AuthContextValue>({
  user: null,
  isLoading: true,
  error: null,
});
