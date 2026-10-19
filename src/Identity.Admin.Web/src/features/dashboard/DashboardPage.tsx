import { ReloadOutlined } from '@ant-design/icons';
import { Button, Typography } from 'antd';
import { PageHeader } from '@/components/PageHeader';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { DashboardMetrics } from './DashboardMetrics';
import { RegistrationChart } from './RegistrationChart';
import { formatInstant } from './format';
import { useDashboard } from './useDashboard';

export function DashboardPage() {
  const query = useDashboard();
  const refresh = () => {
    void query.refetch();
  };
  return (
    <>
      <PageHeader
        title="Dashboard"
        description="An overview of your Identity service."
        actions={
          !query.isPending &&
          !query.isError && (
            <Button
              aria-label="Refresh dashboard"
              icon={<ReloadOutlined aria-hidden="true" />}
              onClick={refresh}
              loading={query.isFetching}
              disabled={query.isFetching}
            >
              Refresh
            </Button>
          )
        }
      />
      {query.isPending ? (
        <LoadingState title="Loading dashboard…" />
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={refresh} />
      ) : (
        <section aria-label="Dashboard snapshot" aria-busy={query.isFetching}>
          <Typography.Paragraph type="secondary">
            Snapshot as of{' '}
            <time
              dateTime={query.data.asOf}
              title={new Date(query.data.asOf).toISOString()}
            >
              {formatInstant(query.data.asOf)}
            </time>{' '}
            · your local time
          </Typography.Paragraph>
          {query.isFetching && (
            <Typography.Paragraph role="status">
              Refreshing dashboard…
            </Typography.Paragraph>
          )}
          <DashboardMetrics data={query.data} />
          {query.data.totalUsers === 0 && (
            <Typography.Paragraph role="status">
              No users yet. These counts reflect an empty Identity service.
            </Typography.Paragraph>
          )}
          <RegistrationChart data={query.data} />
        </section>
      )}
    </>
  );
}
