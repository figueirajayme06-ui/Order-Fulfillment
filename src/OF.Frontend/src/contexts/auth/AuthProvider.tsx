import { useEffect, useState, type FC, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { AuthContext, type AuthContextValue, type AuthFailureReason } from "./AuthContext";
import type { UserInfo } from "../../types";
import api from "../../services/api";

interface Props {
  children: ReactNode;
}

export const AuthProvider: FC<Props> = ({ children }) => {
  const { i18n } = useTranslation();
  const [state, setState] = useState<AuthContextValue>({
    user: null,
    isLoading: true,
    error: null,
  });

  useEffect(() => {
    const fetchUser = async () => {
      try {
        const response = await api.get<UserInfo>("/api/auth/me");
        const user = response.data;
        setState({ user, isLoading: false, error: null });

        if (user.language) {
          await i18n.changeLanguage(user.language);
        }
      } catch (error) {
        setState({ user: null, isLoading: false, error: classifyAuthFailure(error) });
      }
    };

    fetchUser();
  }, [i18n]);

  return <AuthContext value={state}>{children}</AuthContext>;
};

function classifyAuthFailure(error: unknown): AuthFailureReason {
  const response = (error as { response?: { status?: number; data?: { code?: unknown } } })?.response;

  if (response?.status === 403 && response.data?.code === "user_not_provisioned") {
    return "notProvisioned";
  }

  if (response?.status === 401) {
    return "authenticationRequired";
  }

  return "serviceUnavailable";
}
