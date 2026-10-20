import { Card, theme, Typography } from 'antd';
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { EmptyState } from '@/components/EmptyState';
import type { Dashboard } from './api';
import { formatDay, formatFullDay, numberFormat } from './format';

export function RegistrationChart({ data }: { data: Dashboard }) {
  const { token } = theme.useToken();
  const points = data.registrationTrend;
  const empty = points.every((point) => point.count === 0);
  return (
    <Card className="dashboard-trend">
      <Typography.Title level={2}>Registrations</Typography.Title>
      <Typography.Paragraph type="secondary">
        Last 30 calendar days ·{' '}
        {formatFullDay(
          new Date(data.registrationTrendFrom).toISOString().slice(0, 10),
        )}{' '}
        –{' '}
        {formatFullDay(
          new Date(Date.parse(data.registrationTrendTo) - 86_400_000)
            .toISOString()
            .slice(0, 10),
        )}{' '}
        (UTC)
      </Typography.Paragraph>
      <Typography.Paragraph type="secondary">
        Users registered per UTC day. Today is partial, through the snapshot
        time.
      </Typography.Paragraph>
      {points.length === 0 ? (
        <EmptyState
          title="Registration data unavailable"
          description="Identity returned no daily registration values."
        />
      ) : (
        <>
          {empty && (
            <Typography.Paragraph role="status">
              No registrations in this period.
            </Typography.Paragraph>
          )}
          <div
            className="registration-chart"
            role="group"
            aria-label="Daily registrations chart"
          >
            <ResponsiveContainer
              width="100%"
              height={280}
              minWidth={0}
              initialDimension={{ width: 300, height: 280 }}
            >
              <LineChart
                data={points}
                accessibilityLayer
                margin={{ top: 12, right: 16, bottom: 8, left: 0 }}
                aria-label="Registrations per UTC day. Use arrow keys to explore."
              >
                <CartesianGrid
                  vertical={false}
                  stroke={token.colorBorderSecondary}
                />
                <XAxis
                  dataKey="date"
                  tickFormatter={formatDay}
                  minTickGap={36}
                  tick={{
                    fill: token.colorTextSecondary,
                    fontSize: token.fontSizeSM,
                  }}
                  axisLine={false}
                  tickLine={false}
                />
                <YAxis
                  allowDecimals={false}
                  domain={[0, 'auto']}
                  width={48}
                  tickFormatter={(value: number) => numberFormat.format(value)}
                  tick={{
                    fill: token.colorTextSecondary,
                    fontSize: token.fontSizeSM,
                  }}
                  axisLine={false}
                  tickLine={false}
                />
                <Tooltip
                  labelFormatter={(value) =>
                    typeof value === 'string'
                      ? `${formatFullDay(value)} (UTC)`
                      : ''
                  }
                  formatter={(value) => [
                    typeof value === 'number' ? numberFormat.format(value) : '',
                    'Registrations',
                  ]}
                  contentStyle={{
                    borderRadius: token.borderRadius,
                    borderColor: token.colorBorder,
                    background: token.colorBgContainer,
                    color: token.colorText,
                    boxShadow: token.boxShadowSecondary,
                  }}
                />
                <Line
                  type="linear"
                  dataKey="count"
                  name="Registrations"
                  stroke={token.colorPrimary}
                  strokeWidth={2}
                  dot={false}
                  activeDot={{ r: 4 }}
                  isAnimationActive={false}
                />
              </LineChart>
            </ResponsiveContainer>
          </div>
          <details className="registration-values">
            <summary>View daily values</summary>
            <table>
              <caption>Daily registrations · UTC</caption>
              <thead>
                <tr>
                  <th scope="col">Date (UTC)</th>
                  <th scope="col">Registrations</th>
                </tr>
              </thead>
              <tbody>
                {points.map((point) => (
                  <tr key={point.date}>
                    <th scope="row">
                      <time dateTime={point.date}>
                        {formatFullDay(point.date)}
                      </time>
                    </th>
                    <td>{numberFormat.format(point.count)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </details>
        </>
      )}
    </Card>
  );
}
