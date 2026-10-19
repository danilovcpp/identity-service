import { Navigate, Route, Routes } from 'react-router-dom';
import { lazy, Suspense } from 'react';
import { AdminLayout } from '@/components/layout/AdminLayout';
import { PublicLayout } from '@/components/layout/PublicLayout';
import { LoadingState } from '@/components/LoadingState';
import { NotFoundPage } from '@/pages/NotFoundPage';
import { AuthProvider } from '@/features/auth/AuthProvider';
import { AuthGate } from '@/features/auth/AuthGate';
import { LoginPage } from '@/features/auth/LoginPage';
import { SessionControls } from '@/features/auth/SessionControls';

const DashboardPage = lazy(() =>
  import('@/features/dashboard/DashboardPage').then((module) => ({
    default: module.DashboardPage,
  })),
);
const UsersPage = lazy(() =>
  import('@/features/users/UsersPage').then((module) => ({
    default: module.UsersPage,
  })),
);
const UserDetailsPage = lazy(() =>
  import('@/features/users/UserDetailsPage').then((module) => ({
    default: module.UserDetailsPage,
  })),
);

export function AppRoutes() {
  return (
    <AuthProvider>
      <Routes>
        <Route element={<AuthGate />}>
          <Route
            path="/admin"
            element={<AdminLayout headerActions={<SessionControls />} />}
          >
            <Route
              index
              element={
                <Suspense
                  fallback={<LoadingState title="Loading dashboard…" />}
                >
                  <DashboardPage />
                </Suspense>
              }
            />
            <Route
              path="users"
              element={
                <Suspense fallback={<LoadingState title="Loading users…" />}>
                  <UsersPage />
                </Suspense>
              }
            />
            <Route
              path="users/:userId"
              element={
                <Suspense fallback={<LoadingState title="Loading user…" />}>
                  <UserDetailsPage />
                </Suspense>
              }
            />
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
