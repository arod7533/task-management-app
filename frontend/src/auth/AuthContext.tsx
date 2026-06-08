import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import * as authApi from "../api/auth";
import { onUnauthorized } from "../api/client";
import { clearSession, getSession, setSession } from "./token";

type Session = { email: string };

interface AuthValue {
  session: Session | null;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSessionState] = useState<Session | null>(() => {
    const s = getSession();
    return s ? { email: s.email } : null;
  });

  // The API client clears the localStorage token on a 401; mirror that here
  // so the UI gates back to the login screen on the next render.
  useEffect(() => onUnauthorized(() => setSessionState(null)), []);

  const login = useCallback(async (email: string, password: string) => {
    const resp = await authApi.login({ email, password });
    setSession({ token: resp.token, email: resp.email });
    setSessionState({ email: resp.email });
  }, []);

  const register = useCallback(async (email: string, password: string) => {
    const resp = await authApi.register({ email, password });
    setSession({ token: resp.token, email: resp.email });
    setSessionState({ email: resp.email });
  }, []);

  const logout = useCallback(() => {
    clearSession();
    setSessionState(null);
  }, []);

  const value = useMemo<AuthValue>(() => ({ session, login, register, logout }),
    [session, login, register, logout]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthValue {
  const v = useContext(AuthContext);
  if (!v) throw new Error("useAuth must be used inside <AuthProvider>");
  return v;
}
