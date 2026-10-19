import { Button, Space, Table, Tag, Typography, type TableProps } from 'antd';
import { LocalDateTime } from '@/components/LocalDateTime';
import type { UserSession } from './api';

export function SessionsTable({
  items,
  onRevoke,
}: {
  items: UserSession[];
  onRevoke?: (session: UserSession) => void;
}) {
  const columns: TableProps<UserSession>['columns'] = [
    {
      title: 'Session / client',
      key: 'session',
      width: 280,
      render: (_, item) => (
        <Space orientation="vertical" size="small">
          <Typography.Text code>{item.id}</Typography.Text>
          <Typography.Text>{item.clientId}</Typography.Text>
          {item.userAgent && (
            <Typography.Text type="secondary" title={item.userAgent}>
              {item.userAgent}
            </Typography.Text>
          )}
        </Space>
      ),
    },
    {
      title: 'State',
      key: 'state',
      width: 110,
      render: (_, item) =>
        item.revokedAt ? (
          <Tag>Revoked</Tag>
        ) : item.isActive ? (
          <Tag color="success">Active</Tag>
        ) : (
          <Tag>
            {Date.parse(item.absoluteExpiresAt) <= Date.now()
              ? 'Expired'
              : 'Inactive'}
          </Tag>
        ),
    },
    {
      title: 'Created',
      key: 'created',
      width: 165,
      render: (_, item) => <LocalDateTime value={item.createdAt} />,
    },
    {
      title: 'Last used',
      key: 'used',
      width: 165,
      render: (_, item) => <LocalDateTime value={item.lastUsedAt} />,
    },
    {
      title: 'Expires',
      key: 'expires',
      width: 165,
      render: (_, item) => <LocalDateTime value={item.absoluteExpiresAt} />,
    },
    {
      title: 'Revoked',
      key: 'revoked',
      width: 190,
      render: (_, item) =>
        item.revokedAt ? (
          <Space orientation="vertical" size="small">
            <LocalDateTime value={item.revokedAt} />
            {item.revokeReason && (
              <Typography.Text type="secondary">
                {item.revokeReason}
              </Typography.Text>
            )}
          </Space>
        ) : (
          'Not revoked'
        ),
    },
    ...(onRevoke
      ? [
          {
            title: 'Actions',
            key: 'actions',
            width: 130,
            render: (_: unknown, item: UserSession) =>
              item.isActive ? (
                <Button
                  danger
                  aria-label={`Revoke session ${item.id}`}
                  onClick={() => onRevoke(item)}
                >
                  Revoke
                </Button>
              ) : null,
          },
        ]
      : []),
  ];
  return (
    <div
      className="users-table-scroll session-table"
      role="region"
      aria-label="Sessions table · scroll horizontally for all columns"
      tabIndex={0}
    >
      <Table<UserSession>
        rowKey="id"
        columns={columns}
        dataSource={items}
        pagination={false}
        tableLayout="fixed"
        style={{ minWidth: 1200 }}
      />
    </div>
  );
}
