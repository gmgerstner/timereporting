import { useCallback, useMemo, useState, type ReactNode } from 'react';
import { api } from '../api/client';
import {
  clearCredentials,
  getStoredUsername,
  hasValidSession,
  setCredentials,
} from '../api/storage';
import type { LoginCredentials, User } from '../models';
import { AuthContext, type AuthContextValue } from './AuthContext';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [username, setUsername] = useState(() => (hasValidSession() ? getStoredUsername() : ''));

  const login = useCallback(async (credentials: LoginCredentials): Promise<User> => {
    const user = await api.login(credentials);
    setCredentials(user);
    setUsername(user.username);
    return user;
  }, []);

  const logout = useCallback(() => {
    clearCredentials();
    setUsername('');
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({ username, isAuthenticated: username !== '', login, logout }),
    [username, login, logout],
  );

  return <AuthContext value={value}>{children}</AuthContext>;
}
