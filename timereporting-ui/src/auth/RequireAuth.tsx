import { useEffect, type ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { hasValidSession } from '../api/storage';
import { useAuth } from './useAuth';

/**
 * Route wrapper equivalent to the Angular `AuthGuard`: it sends visitors to the
 * login page unless a stored, unexpired token is present.
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { isAuthenticated, logout } = useAuth();
  const location = useLocation();
  const sessionValid = hasValidSession();

  useEffect(() => {
    // The token outlived the session in memory (e.g. the tab was left open).
    if (isAuthenticated && !sessionValid) {
      logout();
    }
  }, [isAuthenticated, sessionValid, logout]);

  if (!sessionValid) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  return <>{children}</>;
}
