import { useRef, useState, type ComponentRef } from 'react';
import {
  App,
  Button,
  Card,
  Flex,
  Pagination,
  Select,
  Space,
  Typography,
} from 'antd';
import { useAuth } from '@/features/auth/authContext';
import { ConfirmAction } from '@/components/ConfirmAction';
import { LoadingState } from '@/components/LoadingState';
import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import {
  maxPage,
  pageSize,
  type RevokeCommand,
  type SessionState,
} from './api';
import { useUserSessions } from './useUserSessions';
import { useRevokeSessions } from './useRevokeSessions';
import { SessionCommandError } from './SessionCommandError';
import { SessionsTable } from './SessionsTable';

export function UserSessions({
  userId,
  email,
}: {
  userId: string;
  email: string;
}) {
  const [page, setPage] = useState(1);
  const [state, setState] = useState<SessionState>('all');
  const [selection, setSelection] = useState<RevokeCommand | null>(null);
  const refreshButton = useRef<ComponentRef<typeof Button>>(null);
  const returnToRefresh = useRef(false);
  const { canWrite } = useAuth();
  const { message } = App.useApp();
  const query = useUserSessions(userId, page, state);
  const command = useRevokeSessions();
  const refresh = () => {
    void query.refetch();
  };
  const select = (value: RevokeCommand) => {
    command.reset();
    setSelection(value);
  };
  async function confirm() {
    if (!selection || !canWrite) return;
    try {
      if (await command.execute(selection)) {
        returnToRefresh.current = true;
        setSelection(null);
        void message.success(
          selection.kind === 'one'
            ? 'Session revoked.'
            : 'All sessions revoked.',
        );
      }
    } catch {
      /* Keep the error in the confirmation; never replay an unsafe command. */
    }
  }
  return (
    <section aria-label="User sessions" className="user-activity">
      <Card title={<Typography.Title level={2}>Sessions</Typography.Title>}>
        <Typography.Paragraph type="secondary">
          Refresh sessions, newest first. Dates are local; hover for UTC.
          Administrator browser sessions are not listed.
        </Typography.Paragraph>
        <Flex
          wrap
          gap="middle"
          justify="space-between"
          align="center"
          className="user-sessions-toolbar"
        >
          <Space wrap>
            <label htmlFor={`session-state-${userId}`}>Session state</label>
            <Select<SessionState>
              id={`session-state-${userId}`}
              value={state}
              virtual={false}
              options={[
                { value: 'all', label: 'All states' },
                { value: 'active', label: 'Active' },
                { value: 'revoked', label: 'Revoked' },
                { value: 'expired', label: 'Expired' },
              ]}
              onChange={(value) => {
                setState(value);
                setPage(1);
              }}
            />
          </Space>
          <Space wrap>
            <Button
              ref={refreshButton}
              aria-label="Refresh sessions"
              onClick={refresh}
              loading={query.isFetching}
            >
              Refresh sessions
            </Button>
            {canWrite && (
              <Button danger onClick={() => select({ kind: 'all', userId })}>
                Revoke all sessions
              </Button>
            )}
          </Space>
        </Flex>
        {query.isPending ? (
          <LoadingState title="Loading sessions…" />
        ) : query.isError ? (
          <ErrorState error={query.error} onRetry={refresh} />
        ) : (
          <div aria-busy={query.isFetching}>
            <Typography.Paragraph role="status">
              {query.data.total.toLocaleString('en-US')} matching refresh
              sessions{query.isFetching ? ' · Refreshing…' : ''}
            </Typography.Paragraph>
            {query.data.items.length === 0 ? (
              <EmptyState
                title="No sessions found"
                description="There are no retained refresh sessions matching this state."
              />
            ) : (
              <SessionsTable
                items={query.data.items}
                onRevoke={
                  canWrite
                    ? (session) =>
                        select({ kind: 'one', userId, sessionId: session.id })
                    : undefined
                }
              />
            )}
            {query.data.items.length === 0 && page > 1 && (
              <Button onClick={() => setPage(1)}>
                Go to first session page
              </Button>
            )}
            {query.data.total > pageSize && (
              <nav
                className="users-pagination"
                aria-label="Sessions pagination"
              >
                <Pagination
                  current={page}
                  pageSize={pageSize}
                  total={Math.min(query.data.total, maxPage * pageSize)}
                  showSizeChanger={false}
                  responsive
                  showLessItems
                  onChange={setPage}
                />
              </nav>
            )}
          </div>
        )}
        {canWrite && (
          <ConfirmAction
            open={selection !== null}
            title={
              selection?.kind === 'one'
                ? 'Revoke session?'
                : 'Revoke all sessions?'
            }
            confirmText={
              selection?.kind === 'one'
                ? 'Confirm revoke session'
                : 'Confirm revoke all'
            }
            danger
            pending={command.pending}
            description={
              selection?.kind === 'one' ? (
                <>
                  Revoke refresh session{' '}
                  <Typography.Text code>{selection.sessionId}</Typography.Text>{' '}
                  for <Typography.Text strong>{email}</Typography.Text>? This
                  stops further refreshes for this session. Other sessions are
                  unaffected. External access tokens can remain valid until they
                  expire.
                </>
              ) : (
                <>
                  Revoke all refresh and administrator browser sessions for{' '}
                  <Typography.Text strong>{email}</Typography.Text>? This
                  includes your browser session if you are managing your own
                  account. The account stays enabled and can sign in again.
                  External access tokens can remain valid until they expire.
                </>
              )
            }
            afterClose={() => {
              if (returnToRefresh.current) {
                refreshButton.current?.focus();
                returnToRefresh.current = false;
              }
            }}
            onConfirm={() => void confirm()}
            onCancel={() => setSelection(null)}
          >
            <SessionCommandError error={command.error} />
          </ConfirmAction>
        )}
      </Card>
    </section>
  );
}
