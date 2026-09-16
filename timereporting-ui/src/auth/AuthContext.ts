import { createContext } from 'react';
import type { LoginCredentials, User } from '../models';

export interface AuthContextValue {
  /** Username of the signed-in user, or "" when signed out. */
  username: string;
  /** True when the signed-in user may view other users' data. */
  isAdmin: boolean;
  isAuthenticated: boolean;
  login: (credentials: LoginCredentials) => Promise<User>;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);
