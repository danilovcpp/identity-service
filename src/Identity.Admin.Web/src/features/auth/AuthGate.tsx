import { Button, Result } from 'antd';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { PublicLayout } from '@/components/layout/PublicLayout';
import { useAuth } from './authContext';
import { safeReturnPath } from './returnPath';
import { SessionControls } from './SessionControls';
import { PageTitle } from '@/components/PageTitle';

export function SessionState() {
  const auth = useAuth();
  if (auth.status === 'loading')
    return <LoadingState title="Checking your session…" />;
  if (auth.status === 'forbidden')
    return (
      <Result
        status="403"
        title={<PageTitle>Access denied</PageTitle>}
        subTitle="Your session does not have access to Identity administration."
        extra={
          <>
            <Button onClick={auth.retry}>Check access again</Button>
            <SessionControls />
          </>
        }
      />
    );
  return <ErrorState error={auth.error} onRetry={auth.retry} />;
}

export function AuthGate() {
  const auth = useAuth();
  const location = useLocation();
  if (auth.status === 'authenticated') return <Outlet />;
  if (auth.status === 'unauthenticated')
    return (
      <Navigate
        to="/admin/login"
        replace
        state={{
          returnTo: safeReturnPath(location.pathname + location.search),
        }}
      />
    );
  return (
    <PublicLayout>
      <SessionState />
    </PublicLayout>
  );
}
