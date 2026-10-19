import { LogoutOutlined } from '@ant-design/icons';
import { Alert, Button, Tag, Typography } from 'antd';
import { useState } from 'react';
import { ApiError, ClientError } from '@/api/errors';
import { useAuth } from './authContext';

export function SessionControls() {
  const { profile, signOut, pending } = useAuth();
  const [error, setError] = useState<unknown>(null);
  return (
    <div className="session-controls">
      {profile && (
        <div className="session-identity">
          <Typography.Text className="session-name" title={profile.email}>
            {profile.displayName || profile.email}
          </Typography.Text>
          <Tag>
            {profile.role === 'IdentityAdministrator'
              ? 'Administrator'
              : 'Read only'}
          </Tag>
        </div>
      )}
      <Button
        aria-label="Sign out"
        icon={<LogoutOutlined aria-hidden="true" />}
        loading={pending}
        disabled={pending}
        onClick={() => {
          setError(null);
          void signOut().catch((error: unknown) => setError(error));
        }}
      >
        Sign out
      </Button>
      {error !== null && (
        <Alert
          type="error"
          showIcon
          title="Sign out failed"
          description={
            error instanceof ApiError || error instanceof ClientError
              ? error.message
              : 'Please try again.'
          }
          closable
          onClose={() => setError(null)}
        />
      )}
    </div>
  );
}
