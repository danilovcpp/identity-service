import { AppErrorBoundary } from '@/components/AppErrorBoundary';
import { AppRoutes } from '@/app/routes';

export function App() {
  return (
    <AppErrorBoundary>
      <AppRoutes />
    </AppErrorBoundary>
  );
}
