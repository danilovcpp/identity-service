import { Button, Card, Typography } from 'antd';
import { LocalDateTime } from '@/components/LocalDateTime';
import { LoadingState } from '@/components/LoadingState';
import { ErrorState } from '@/components/ErrorState';
import { EmptyState } from '@/components/EmptyState';
import { useUserActivity } from './useUserActivity';

export function UserActivity({ userId }: { userId: string }) {
  const query = useUserActivity(userId);
  const refresh = () => {
    void query.refetch();
  };
  return (
    <section aria-label="Recent security activity" className="user-activity">
      <Card
        title={
          <Typography.Title level={2}>
            Recent security activity
          </Typography.Title>
        }
      >
        <div className="user-details-refresh">
          <Button
            onClick={refresh}
            disabled={query.isFetching}
            loading={query.isFetching}
            aria-label="Refresh security activity"
          >
            Refresh
          </Button>
        </div>
        <Typography.Paragraph type="secondary">
          Latest 10 retained events for this account, newest first. Times are
          local; hover for UTC.
        </Typography.Paragraph>
        {query.isPending ? (
          <LoadingState title="Loading security activity…" />
        ) : query.isError ? (
          <ErrorState error={query.error} onRetry={refresh} />
        ) : query.data.length === 0 ? (
          <EmptyState
            title="No recent security activity"
            description="There are no retained events for this account."
          />
        ) : (
          <ol
            className="user-events"
            aria-label="Security events"
            aria-busy={query.isFetching}
          >
            {query.data.map((event) => (
              <li key={event.id}>
                <Typography.Text code>{event.eventType}</Typography.Text>
                <LocalDateTime value={event.occurredAt} />
              </li>
            ))}
          </ol>
        )}
      </Card>
    </section>
  );
}
