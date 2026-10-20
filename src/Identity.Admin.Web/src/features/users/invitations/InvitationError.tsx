import { ApiError } from '@/api/errors';
import { CommandError } from '@/components/CommandError';

export function InvitationError({ error }: { error: unknown }) {
  if (!error) return null;
  const description =
    error instanceof ApiError && error.status === 409
      ? error.code === 'admin_email_conflict'
        ? 'An account with this email already exists. Find the account in Users; only invited accounts can receive another invitation.'
        : 'The account state has changed. Refresh the account before trying again.'
      : error instanceof ApiError && error.code === 'invitations_unavailable'
        ? 'Invitations are currently unavailable. Contact your Identity operator.'
        : undefined;
  return (
    <CommandError
      error={error}
      title="Unable to request invitation"
      description={description}
      recovery="Check Users or recent security activity before submitting again."
    />
  );
}
