import { Navigate, Route, Routes } from 'react-router-dom';
import { AdminLayout } from '@/components/layout/AdminLayout';
import { PublicLayout } from '@/components/layout/PublicLayout';
import { ConsolePage } from '@/pages/ConsolePage';
import { NotFoundPage } from '@/pages/NotFoundPage';
import { AuthProvider } from '@/features/auth/AuthProvider';
import { AuthGate } from '@/features/auth/AuthGate';
import { LoginPage } from '@/features/auth/LoginPage';
import { SessionControls } from '@/features/auth/SessionControls';

export function AppRoutes() {
  return (
    <AuthProvider>
      <Routes>
        <Route element={<AuthGate />}>
          <Route
            path="/admin"
            element={<AdminLayout headerActions={<SessionControls />} />}
          >
            <Route index element={<ConsolePage />} />
            <Route path="*" element={<NotFoundPage />} />
          </Route>
        </Route>
        <Route element={<PublicLayout />}>
          <Route path="/admin/login" element={<LoginPage />} />
          <Route path="/" element={<Navigate to="/admin" replace />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </AuthProvider>
  );
}
