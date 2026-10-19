import { Alert } from 'antd';
import { ApiError, ClientError } from '@/api/errors';

export function StatusCommandError({ error }: { error: unknown }) {
  if (!error) return null;
  const description =
    error instanceof ApiError && error.code === 'last_administrator'
      ? 'The last active administrator cannot be disabled. Another active administrator must be retained.'
      : error instanceof ApiError && error.status === 409
        ? 'The account state has changed. Refresh the account before trying again.'
        : error instanceof ApiError && error.code === 'csrf_invalid'
          ? 'Your security check expired. Submit again to request a new check.'
          : error instanceof ClientError &&
              ['network', 'timeout'].includes(error.kind)
            ? 'The result could not be confirmed. The request may have completed. Refresh the account before submitting again.'
            : error instanceof ApiError || error instanceof ClientError
              ? error.message
              : 'Identity could not update the account.';
  return (
    <Alert
      type="error"
      showIcon
      title="Unable to update account"
      description={description}
    />
  );
}
