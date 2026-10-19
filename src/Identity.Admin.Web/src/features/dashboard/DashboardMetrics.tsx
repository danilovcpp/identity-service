import { Card, Col, Row, Statistic, Typography } from 'antd';
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
    <Row gutter={[16, 16]} className="dashboard-metrics">
      {(Object.keys(metricLabels) as Metric[]).map((metric) => (
        <Col
          key={metric}
          xs={metric === 'totalUsers' ? 24 : 12}
          xl={metric === 'totalUsers' ? 12 : 6}
        >
          <section
            aria-label={metricLabels[metric]}
            className="dashboard-metric-card"
          >
            <Card className="dashboard-metric-card">
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
        </Col>
      ))}
    </Row>
  );
}
