import { Card, Statistic, Typography } from 'antd';
import type { Dashboard, Metric } from './api';
import { numberFormat } from './format';

const metricLabels = {
  totalUsers: 'Total users',
  activeUsers: 'Active users',
  activeSessions: 'Active sessions',
  pendingVerificationUsers: 'Pending verification',
  invitedUsers: 'Invited',
  disabledUsers: 'Disabled',
  deactivatedUsers: 'Deactivated',
} satisfies Record<Metric, string>;

export function DashboardMetrics({ data }: { data: Dashboard }) {
  return (
    <div className="dashboard-metrics">
      {(Object.keys(metricLabels) as Metric[]).map((metric) => (
        <section
          key={metric}
          aria-label={metricLabels[metric]}
          className={metric === 'totalUsers' ? 'dashboard-total' : undefined}
        >
          <Card size="small" className="dashboard-metric-card">
            <Statistic
              title={metricLabels[metric]}
              value={data[metric]}
              formatter={() => numberFormat.format(data[metric])}
            />
            {metric === 'totalUsers' && (
              <Typography.Text type="secondary">
                All account statuses
              </Typography.Text>
            )}
            {metric === 'activeSessions' && (
              <Typography.Text type="secondary">
                Native / BFF · excludes console sessions
              </Typography.Text>
            )}
          </Card>
        </section>
      ))}
    </div>
  );
}
