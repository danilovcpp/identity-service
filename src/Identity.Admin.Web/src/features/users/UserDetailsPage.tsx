import { useEffect } from 'react';
import { ReloadOutlined } from '@ant-design/icons';
import { Button, Result, Typography } from 'antd';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { ApiError } from '@/api/errors';
import { PageHeader } from '@/components/PageHeader';
import { LoadingState } from '@/components/LoadingState';
import { ErrorState } from '@/components/ErrorState';
import { parseUsersParameters, usersSearchParameters } from './parameters';
import { isUserId } from './userId';
import { useUserDetails } from './useUserDetails';
import { UserProfile } from './UserProfile';
import { UserActivity } from './UserActivity';
import { ResendInvitation } from './invitations/ResendInvitation';
import { AccountStatusAction } from './accountStatus/AccountStatusAction';

function Details({ userId }: { userId: string }) {
  const query = useUserDetails(userId);
  if (query.isPending) return <LoadingState title="Loading user…" />;
  if (query.isError)
    return query.error instanceof ApiError && query.error.status === 404 ? (
      <Result
        status="404"
        title={<Typography.Title level={2}>User not found</Typography.Title>}
        subTitle="This account does not exist or is no longer available."
      />
    ) : (
      <ErrorState
        error={query.error}
        onRetry={() => {
          void query.refetch();
        }}
      />
    );
  return (
    <div aria-busy={query.isFetching}>
      <div className="user-details-refresh">
        <AccountStatusAction
          userId={userId}
          email={query.data.email}
          status={query.data.status}
        />
        <ResendInvitation userId={userId} status={query.data.status} />
        <Button
          icon={<ReloadOutlined aria-hidden="true" />}
          aria-label="Refresh user details"
          loading={query.isFetching}
          disabled={query.isFetching}
          onClick={() => {
            void query.refetch();
          }}
        >
          Refresh account
        </Button>
      </div>
      <UserProfile data={query.data} />
      <UserActivity userId={userId} />
    </div>
  );
}

export function UserDetailsPage() {
  const { userId } = useParams();
  const [query, setQuery] = useSearchParams();
  const canonical = usersSearchParameters(
    parseUsersParameters(query),
  ).toString();
  const backTo = '/admin/users' + (canonical ? `?${canonical}` : '');
  useEffect(() => {
    if (query.toString() !== canonical) setQuery(canonical, { replace: true });
  }, [query, canonical, setQuery]);
  return (
    <>
      <PageHeader
        title="User details"
        description="Review account information and recent security activity."
        breadcrumbs={[
          { title: <Link to={backTo}>Users</Link> },
          { title: 'User details' },
        ]}
        actions={<Link to={backTo}>Back to users</Link>}
      />
      {isUserId(userId) ? (
        <Details key={userId.toLowerCase()} userId={userId.toLowerCase()} />
      ) : (
        <Result
          status="warning"
          title={<Typography.Title level={2}>Invalid user ID</Typography.Title>}
          subTitle="Use a nonempty UUID to open an account."
        />
      )}
    </>
  );
}
