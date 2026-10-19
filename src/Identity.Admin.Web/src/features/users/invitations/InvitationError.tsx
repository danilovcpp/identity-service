import { Alert } from 'antd';
import { ApiError, ClientError } from '@/api/errors';

export function InvitationError({ error }: { error: unknown }) {
  if (!error) return null;
  const description =
    error instanceof ApiError && error.status === 409
      ? error.code === 'admin_email_conflict'
        ? 'An account with this email already exists. Find the account in Users; only invited accounts can receive another invitation.'
        : 'The account state has changed. Refresh the account before trying again.'
      : error instanceof ApiError && error.code === 'invitations_unavailable'
        ? 'Invitations are currently unavailable. Contact your Identity operator.'
        : error instanceof ApiError && error.code === 'csrf_invalid'
          ? 'Your security check expired. Submit again to request a new check.'
          : error instanceof ClientError &&
              ['network', 'timeout'].includes(error.kind)
            ? 'The result could not be confirmed. The request may have been accepted. Check Users or recent security activity before submitting again.'
            : error instanceof ApiError || error instanceof ClientError
              ? error.message
              : 'Identity could not complete the request.';
  return (
    <Alert
      type="error"
      showIcon
      title="Unable to request invitation"
      description={description}
    />
  );
}
