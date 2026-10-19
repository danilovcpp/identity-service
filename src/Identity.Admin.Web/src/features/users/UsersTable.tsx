import { Table, type TableProps } from 'antd';
import { Link } from 'react-router-dom';
import { StatusTag } from '@/components/StatusTag';
import { LocalDateTime } from '@/components/LocalDateTime';
import type { UserListItem } from './api';
import { usersSearchParameters, type UsersParameters } from './parameters';

export function UsersTable({
  items,
  parameters,
  onSort,
}: {
  items: UserListItem[];
  parameters: UsersParameters;
  onSort: (sort: Pick<UsersParameters, 'sortBy' | 'sortDirection'>) => void;
}) {
  const order = (key: UsersParameters['sortBy']) =>
    parameters.sortBy === key
      ? parameters.sortDirection === 'asc'
        ? 'ascend'
        : 'descend'
      : null;
  const columns: TableProps<UserListItem>['columns'] = [
    {
      title: 'Email',
      dataIndex: 'email',
      key: 'email',
      width: 220,
      ellipsis: true,
      sorter: true,
      sortOrder: order('email'),
    },
    {
      title: 'Display name',
      dataIndex: 'displayName',
      key: 'displayName',
      width: 140,
      ellipsis: true,
      render: (value: string | null) => value || 'Not provided',
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      width: 160,
      sorter: true,
      sortOrder: order('status'),
      render: (value: string) => <StatusTag status={value} />,
    },
    {
      title: 'Email verified',
      dataIndex: 'emailVerified',
      key: 'emailVerified',
      width: 120,
      render: (value: boolean) => (value ? 'Verified' : 'Not verified'),
    },
    {
      title: 'Created · local time',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 170,
      sorter: true,
      sortOrder: order('createdAt'),
      render: (value: string) => <LocalDateTime value={value} />,
    },
    {
      title: 'Details',
      key: 'details',
      width: 110,
      render: (_, item) => (
        <Link
          className="user-details-link"
          to={{
            pathname: `/admin/users/${item.id}`,
            search: usersSearchParameters(parameters).toString(),
          }}
          aria-label={`View details for ${item.email}`}
        >
          View details
        </Link>
      ),
    },
  ];
  const change: NonNullable<TableProps<UserListItem>['onChange']> = (
    _,
    __,
    sorter,
    extra,
  ) => {
    if (extra.action !== 'sort' || Array.isArray(sorter)) return;
    const key = sorter.columnKey;
    if (key === 'createdAt' || key === 'email' || key === 'status') {
      onSort({
        sortBy: key,
        sortDirection: sorter.order === 'ascend' ? 'asc' : 'desc',
      });
    }
  };
  return (
    <div
      className="users-table-scroll"
      role="region"
      aria-label="Users table · scroll horizontally for all columns"
      tabIndex={0}
    >
      <Table<UserListItem>
        rowKey="id"
        columns={columns}
        dataSource={items}
        pagination={false}
        style={{ minWidth: 920 }}
        tableLayout="fixed"
        sortDirections={['ascend', 'descend', 'ascend']}
        onChange={change}
      />
    </div>
  );
}
