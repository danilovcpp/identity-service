import { Alert } from 'antd';
import { ApiError, ClientError } from '@/api/errors';

// Features supply business conflicts and recovery advice; transport/security
// failures share one policy. Never display raw server fields or exceptions.
export function CommandError({
  error,
  title,
  description,
  recovery,
}: {
  error: unknown;
  title: string;
  description?: string;
  recovery: string;
}) {
  if (!error) return null;
  const uncertain =
    (error instanceof ClientError &&
      ['network', 'timeout', 'invalid_response'].includes(error.kind)) ||
    (error instanceof ApiError && error.status >= 500);
  return (
    <Alert
      type="error"
      showIcon
      title={title}
      description={
        description ??
        (error instanceof ApiError && error.code === 'csrf_invalid'
          ? 'Your security check expired. Submit again to request a new check.'
          : uncertain
            ? `The result could not be confirmed. The request may have completed. ${recovery}`
            : error instanceof ApiError || error instanceof ClientError
              ? error.message
              : 'Identity could not complete the request.')
      }
    />
  );
}
