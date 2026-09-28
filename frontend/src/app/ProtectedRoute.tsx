import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useAppStore } from '../store/appStore';

/**
 * Every route is protected by default (00-Frontend-Specs.md, section 9). A session that must change
 * its password reaches only the change-password screen — the API refuses everything else anyway.
 */
export function ProtectedRoute({ children, allowPasswordChange = false }: { children: ReactNode; allowPasswordChange?: boolean }) {
  const accessToken = useAppStore((s) => s.accessToken);
  const passwordChangeRequired = useAppStore((s) => s.passwordChangeRequired);

  if (!accessToken) {
    return <Navigate to="/login" replace />;
  }
  if (passwordChangeRequired && !allowPasswordChange) {
    return <Navigate to="/change-password" replace />;
  }
  return <>{children}</>;
}
