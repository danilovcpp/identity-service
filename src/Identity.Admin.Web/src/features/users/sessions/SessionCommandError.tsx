import { Alert } from 'antd';
import { ApiError, ClientError } from '@/api/errors';

export function SessionCommandError({ error }: { error: unknown }) {
  if (!error) return null;
  const description =
    error instanceof ApiError && error.status === 404
      ? 'The account or session is no longer available. Refresh sessions before trying again.'
      : error instanceof ApiError && error.status === 409
        ? 'The session or account state has changed. Refresh sessions before trying again.'
        : error instanceof ApiError && error.code === 'csrf_invalid'
          ? 'Your security check expired. Submit again to request a new check.'
          : error instanceof ClientError &&
              ['network', 'timeout'].includes(error.kind)
            ? 'The result could not be confirmed. The request may have completed. Refresh sessions before submitting again.'
            : error instanceof ApiError || error instanceof ClientError
              ? error.message
              : 'Identity could not revoke sessions.';
  return (
    <Alert
      type="error"
      showIcon
      title="Unable to revoke sessions"
      description={description}
    />
  );
}
