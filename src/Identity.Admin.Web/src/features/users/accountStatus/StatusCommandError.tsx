import { ApiError } from '@/api/errors';
import { CommandError } from '@/components/CommandError';

export function StatusCommandError({ error }: { error: unknown }) {
  if (!error) return null;
  const description =
    error instanceof ApiError && error.code === 'last_administrator'
      ? 'The last active administrator cannot be disabled. Another active administrator must be retained.'
      : error instanceof ApiError && error.status === 409
        ? 'The account state has changed. Refresh the account before trying again.'
        : undefined;
  return (
    <CommandError
      error={error}
      title="Unable to update account"
      description={description}
      recovery="Refresh the account before submitting again."
    />
  );
}
