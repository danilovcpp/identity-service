import {
  CloseCircleFilled,
  ReloadOutlined,
  SearchOutlined,
} from '@ant-design/icons';
import { Button, Card, Input, Pagination, Select, Typography } from 'antd';
import { PageHeader } from '@/components/PageHeader';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { EmptyState } from '@/components/EmptyState';
import { useUsersParameters } from './useUsersParameters';
import { maxUsersPage, userStatuses } from './parameters';
import { useUsers } from './useUsers';
import { UsersTable } from './UsersTable';
import { InviteUser } from './invitations/InviteUser';

const number = new Intl.NumberFormat('en-US');
export function UsersPage() {
  const { values, update, search, setSearch, reset } = useUsersParameters();
  const query = useUsers(values);
  const refresh = () => {
    void query.refetch();
  };
  const pageSizes = [...new Set([10, 25, 50, 100, values.pageSize])].sort(
    (a, b) => a - b,
  );
  return (
    <>
      <PageHeader
        title="Users"
        description="Find and review Identity accounts."
        actions={
          <div className="user-actions">
            <InviteUser />
            <Button
              icon={<ReloadOutlined aria-hidden="true" />}
              onClick={refresh}
              loading={query.isFetching}
              disabled={query.isFetching}
            >
              Refresh
            </Button>
          </div>
        }
      />
      <Card>
        <div className="users-filters" role="search" aria-label="Filter users">
          <div className="users-search">
            <label htmlFor="users-search">Search users</label>
            <Input
              id="users-search"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              maxLength={200}
              allowClear={{
                clearIcon: <CloseCircleFilled aria-label="Clear search" />,
              }}
              prefix={<SearchOutlined aria-hidden="true" />}
              placeholder="Email or display name"
            />
          </div>
          <div>
            <label htmlFor="users-status">Status</label>
            <Select
              id="users-status"
              virtual={false}
              value={values.status ?? 'all'}
              onChange={(status) =>
                update({
                  status: userStatuses.find((value) => value === status),
                  page: 1,
                })
              }
              options={[
                { value: 'all', label: 'All statuses' },
                ...userStatuses.map((value) => ({
                  value,
                  label:
                    value === 'PendingVerification'
                      ? 'Pending verification'
                      : value,
                })),
              ]}
            />
          </div>
          <div>
            <label htmlFor="users-verified">Email verification</label>
            <Select
              id="users-verified"
              virtual={false}
              value={
                values.emailVerified === undefined
                  ? 'all'
                  : String(values.emailVerified)
              }
              onChange={(value) =>
                update({
                  emailVerified: value === 'all' ? undefined : value === 'true',
                  page: 1,
                })
              }
              options={[
                { value: 'all', label: 'All verification states' },
                { value: 'true', label: 'Verified' },
                { value: 'false', label: 'Not verified' },
              ]}
            />
          </div>
          <Button onClick={reset}>Reset filters</Button>
        </div>
        {query.isPending ? (
          <LoadingState title="Loading users…" />
        ) : query.isError ? (
          <ErrorState error={query.error} onRetry={refresh} />
        ) : (
          <section aria-label="User results" aria-busy={query.isFetching}>
            <Typography.Paragraph role="status">
              {number.format(query.data.total)}{' '}
              {query.data.total === 1 ? 'user' : 'users'} found
              {query.isFetching ? ' · Refreshing…' : ''}
            </Typography.Paragraph>
            {query.data.items.length === 0 ? (
              <EmptyState
                title="No users found"
                description="Try a different search or reset the filters."
              />
            ) : (
              <UsersTable
                items={query.data.items}
                parameters={values}
                onSort={(sort) => update({ ...sort, page: 1 })}
              />
            )}
            {query.data.items.length === 0 && values.page > 1 && (
              <Button onClick={() => update({ page: 1 })}>
                Go to first page
              </Button>
            )}
            <nav className="users-pagination" aria-label="Users pagination">
              <div className="users-page-size">
                <label htmlFor="users-page-size">Rows per page</label>
                <Select<number>
                  id="users-page-size"
                  virtual={false}
                  value={values.pageSize}
                  options={pageSizes.map((size) => ({
                    value: size,
                    label: `${size} / page`,
                  }))}
                  onChange={(pageSize) => update({ page: 1, pageSize })}
                />
              </div>
              <Pagination
                current={values.page}
                pageSize={values.pageSize}
                total={Math.min(
                  query.data.total,
                  maxUsersPage(values.pageSize) * values.pageSize,
                )}
                showSizeChanger={false}
                responsive
                showLessItems
                onChange={(page, pageSize) =>
                  update({
                    page: pageSize !== values.pageSize ? 1 : page,
                    pageSize,
                  })
                }
              />
            </nav>
          </section>
        )}
      </Card>
    </>
  );
}
