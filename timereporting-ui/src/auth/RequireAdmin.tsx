import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from './useAuth';

/**
 * Route wrapper for the admin-only pages. Sits inside `RequireAuth`, which has already
 * established that there is a session at all.
 *
 * This hides the page; it does not secure the data. The endpoints behind it carry
 * `[Authorize(Roles = "Admin")]` and answer 403 whatever the browser does.
 */
export function RequireAdmin({ children }: { children: ReactNode }) {
  const { isAdmin } = useAuth();

  if (!isAdmin) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}
