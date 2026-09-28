import { Route, Routes } from 'react-router-dom';
import { AppLayout } from './app/AppLayout';
import { ProtectedRoute } from './app/ProtectedRoute';
import { LoginPage } from './features/auth/LoginPage';
import { ChangePasswordPage } from './features/auth/ChangePasswordPage';
import { TwoFactorPage } from './features/auth/TwoFactorPage';

/**
 * The real browser location only ever picks between the sign-in screens and the authenticated
 * shell (My Remarks/Remarks2.md, remark 3.4) — once inside, AppLayout owns which screens are
 * open by rendering one workspace tab per entry in app/routeTable.tsx's route list, each against
 * its own tab-scoped location rather than a single nested <Outlet/>.
 */
export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/change-password"
        element={
          <ProtectedRoute allowPasswordChange>
            <ChangePasswordPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/two-factor"
        element={
          <ProtectedRoute>
            <TwoFactorPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/*"
        element={
          <ProtectedRoute>
            <AppLayout />
          </ProtectedRoute>
        }
      />
    </Routes>
  );
}
