import { useEffect } from 'react';
import {
  Button,
  Card,
  Flex,
  Pagination,
  Select,
  Space,
  Typography,
} from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { useLocation, useSearchParams } from 'react-router-dom';
import { PageHeader } from '@/components/PageHeader';
import { LoadingState } from '@/components/LoadingState';
import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { AuditFilters } from './AuditFilters';
import { AuditTable } from './AuditTable';
import {
  auditSearchParameters,
  maxAuditPage,
  parseAuditParameters,
  type AuditParameters,
} from './parameters';
import { useAuditEvents } from './useAuditEvents';

export function AuditPage() {
  const [query, setQuery] = useSearchParams();
  const location = useLocation();
  const values = parseAuditParameters(query);
  const canonical = auditSearchParameters(values).toString();
  useEffect(() => {
    if (query.toString() !== canonical) setQuery(canonical, { replace: true });
  }, [query, canonical, setQuery]);
  const update = (change: Partial<AuditParameters>) =>
    setQuery(auditSearchParameters({ ...values, ...change }));
  const events = useAuditEvents(values);
  const refresh = () => {
    void events.refetch();
  };
  const sizes = [...new Set([10, 25, 50, 100, values.pageSize])].sort(
    (a, b) => a - b,
  );
  return (
    <>
      <PageHeader
        title="Audit log"
        description="Retained Identity security events, newest first. Times are local; hover for UTC."
        actions={
          <Button
            aria-label="Refresh audit log"
            icon={<ReloadOutlined aria-hidden="true" />}
            loading={events.isFetching}
            onClick={refresh}
          >
            Refresh
          </Button>
        }
      />
      <Card>
        <section aria-label="Filter audit events">
          <AuditFilters
            key={location.key}
            values={values}
            onApply={(filters) => update({ ...filters, page: 1 })}
            onReset={() => setQuery('')}
          />
        </section>
        {events.isPending ? (
          <LoadingState title="Loading audit events…" />
        ) : events.isError ? (
          <ErrorState error={events.error} onRetry={refresh} />
        ) : (
          <section aria-label="Audit results" aria-busy={events.isFetching}>
            <Typography.Paragraph role="status">
              {events.data.total.toLocaleString('en-US')} events found
              {events.isFetching ? ' · Refreshing…' : ''}
            </Typography.Paragraph>
            {events.data.items.length === 0 ? (
              <EmptyState
                title="No audit events found"
                description="There are no retained events matching these filters."
              />
            ) : (
              <AuditTable items={events.data.items} />
            )}
            {events.data.items.length === 0 && values.page > 1 && (
              <Button onClick={() => update({ page: 1 })}>
                Go to first audit page
              </Button>
            )}
            <nav aria-label="Audit pagination" className="users-pagination">
              <Flex wrap gap="middle" align="center" justify="space-between">
                <Space>
                  <label htmlFor="audit-page-size">Events per page</label>
                  <Select<number>
                    id="audit-page-size"
                    virtual={false}
                    value={values.pageSize}
                    options={sizes.map((size) => ({
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
                    events.data.total,
                    maxAuditPage(values.pageSize) * values.pageSize,
                  )}
                  showSizeChanger={false}
                  responsive
                  showLessItems
                  onChange={(page) => update({ page })}
                />
              </Flex>
            </nav>
          </section>
        )}
      </Card>
    </>
  );
}
