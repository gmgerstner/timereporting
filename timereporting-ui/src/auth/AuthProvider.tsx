import { useCallback, useMemo, useState, type ReactNode } from 'react';
import { api } from '../api/client';
import {
  clearCredentials,
  getStoredUsername,
  hasValidSession,
  isStoredAdmin,
  setCredentials,
} from '../api/storage';
import type { LoginCredentials, User } from '../models';
import { AuthContext, type AuthContextValue } from './AuthContext';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [username, setUsername] = useState(() => (hasValidSession() ? getStoredUsername() : ''));
  const [isAdmin, setIsAdmin] = useState(() => hasValidSession() && isStoredAdmin());

  const login = useCallback(async (credentials: LoginCredentials): Promise<User> => {
    const user = await api.login(credentials);
    setCredentials(user);
    setUsername(user.username);
    setIsAdmin(user.isAdmin);
    return user;
  }, []);

  const logout = useCallback(() => {
    clearCredentials();
    setUsername('');
    setIsAdmin(false);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({ username, isAdmin, isAuthenticated: username !== '', login, logout }),
    [username, isAdmin, login, logout],
  );

  return <AuthContext value={value}>{children}</AuthContext>;
}
