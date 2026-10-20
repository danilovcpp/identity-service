import { ApiError } from '@/api/errors';
import { CommandError } from '@/components/CommandError';

export function SessionCommandError({ error }: { error: unknown }) {
  if (!error) return null;
  const description =
    error instanceof ApiError && error.status === 404
      ? 'The account or session is no longer available. Refresh sessions before trying again.'
      : error instanceof ApiError && error.status === 409
        ? 'The session or account state has changed. Refresh sessions before trying again.'
        : undefined;
  return (
    <CommandError
      error={error}
      title="Unable to revoke sessions"
      description={description}
      recovery="Refresh sessions before submitting again."
    />
  );
}
