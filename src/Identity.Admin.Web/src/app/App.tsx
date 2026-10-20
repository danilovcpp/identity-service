import { AppErrorBoundary } from '@/components/AppErrorBoundary';
import { AppRoutes } from '@/app/routes';
import type { AccountLink } from '@/features/account/accountLink';

export function App({
  initialAccountLink,
}: {
  initialAccountLink?: AccountLink;
}) {
  return (
    <AppErrorBoundary>
      <AppRoutes initialAccountLink={initialAccountLink} />
    </AppErrorBoundary>
  );
}
