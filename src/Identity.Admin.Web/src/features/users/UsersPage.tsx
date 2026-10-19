import {
  CloseCircleFilled,
  ReloadOutlined,
  SearchOutlined,
} from '@ant-design/icons';
import {
  Button,
  Card,
  Col,
  Flex,
  Form,
  Input,
  Pagination,
  Row,
  Select,
  Space,
  Typography,
} from 'antd';
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
          <Space wrap>
            <InviteUser />
            <Button
              icon={<ReloadOutlined aria-hidden="true" />}
              onClick={refresh}
              loading={query.isFetching}
              disabled={query.isFetching}
            >
              Refresh
            </Button>
          </Space>
        }
      />
      <Card>
        <div role="search" aria-label="Filter users">
          <Form layout="vertical" component="div">
            <Row gutter={16} align="bottom">
              <Col xs={24} md={24} xl={8}>
                <Form.Item label="Search users" htmlFor="users-search">
                  <Input
                    id="users-search"
                    value={search}
                    onChange={(event) => setSearch(event.target.value)}
                    maxLength={200}
                    allowClear={{
                      clearIcon: (
                        <CloseCircleFilled aria-label="Clear search" />
                      ),
                    }}
                    prefix={<SearchOutlined aria-hidden="true" />}
                    placeholder="Email or display name"
                  />
                </Form.Item>
              </Col>
              <Col xs={24} sm={12} xl={6}>
                <Form.Item label="Status" htmlFor="users-status">
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
                </Form.Item>
              </Col>
              <Col xs={24} sm={12} xl={6}>
                <Form.Item label="Email verification" htmlFor="users-verified">
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
                        emailVerified:
                          value === 'all' ? undefined : value === 'true',
                        page: 1,
                      })
                    }
                    options={[
                      { value: 'all', label: 'All verification states' },
                      { value: 'true', label: 'Verified' },
                      { value: 'false', label: 'Not verified' },
                    ]}
                  />
                </Form.Item>
              </Col>
              <Col xs={24} xl={4}>
                <Form.Item>
                  <Button onClick={reset}>Reset filters</Button>
                </Form.Item>
              </Col>
            </Row>
          </Form>
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
              <Flex wrap gap="middle" align="center" justify="space-between">
                <Space>
                  <label htmlFor="users-page-size">Rows per page</label>
                  <Select<number>
                    id="users-page-size"
                    className="users-page-size"
                    virtual={false}
                    value={values.pageSize}
                    options={pageSizes.map((size) => ({
                      value: size,
                      label: `${size} / page`,
                    }))}
                    onChange={(pageSize) => update({ page: 1, pageSize })}
                  />
                </Space>
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
              </Flex>
            </nav>
          </section>
        )}
      </Card>
    </>
  );
}
