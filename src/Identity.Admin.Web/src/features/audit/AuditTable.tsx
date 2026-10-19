import { Space, Table, Typography, type TableProps } from 'antd';
import { Link } from 'react-router-dom';
import { LocalDateTime } from '@/components/LocalDateTime';
import { isUserId } from '@/features/users/userId';
import type { AuditEvent } from './api';
import { eventLabels } from './eventLabels';

function AccountLink({ id }: { id: string | null }) {
  return isUserId(id) ? (
    <Link
      to={`/admin/users/${id.toLowerCase()}`}
      aria-label={`View user ${id}`}
    >
      {id}
    </Link>
  ) : (
    <Typography.Text type="secondary">Not recorded</Typography.Text>
  );
}
export function AuditTable({ items }: { items: AuditEvent[] }) {
  const columns: TableProps<AuditEvent>['columns'] = [
    {
      title: 'Occurred · local time',
      key: 'time',
      width: 180,
      render: (_, event) => <LocalDateTime value={event.occurredAt} />,
    },
    {
      title: 'Event',
      key: 'event',
      width: 250,
      render: (_, event) => (
        <Space orientation="vertical" size="small">
          <Typography.Text>
            {Object.hasOwn(eventLabels, event.eventType)
              ? eventLabels[event.eventType]
              : event.eventType}
          </Typography.Text>
          {Object.hasOwn(eventLabels, event.eventType) && (
            <Typography.Text type="secondary" code>
              {event.eventType}
            </Typography.Text>
          )}
        </Space>
      ),
    },
    {
      title: 'Actor',
      key: 'actor',
      width: 285,
      render: (_, event) => <AccountLink id={event.actorUserId} />,
    },
    {
      title: 'Target / related user',
      key: 'target',
      width: 285,
      render: (_, event) => (
        <Space orientation="vertical" size="small">
          <AccountLink id={event.targetUserId ?? event.userId} />
          {!event.targetUserId && event.userId && (
            <Typography.Text type="secondary">
              Legacy related user
            </Typography.Text>
          )}
        </Space>
      ),
    },
    {
      title: 'Correlation ID',
      key: 'correlation',
      width: 260,
      render: (_, event) => event.correlationId || 'Not recorded',
    },
  ];
  return (
    <div
      className="users-table-scroll audit-table"
      role="region"
      aria-label="Audit table · scroll horizontally for all columns"
      tabIndex={0}
    >
      <Table<AuditEvent>
        rowKey="id"
        columns={columns}
        dataSource={items}
        tableLayout="fixed"
        style={{ minWidth: 1260 }}
        pagination={false}
      />
    </div>
  );
}
