import { apiClient } from '@/api/httpClient';
import type { AccountFlow } from './accountLink';

// Direct transport only: secrets must never become Query mutation variables.
export async function setAccountPassword(
  flow: AccountFlow,
  token: string,
  newPassword: string,
  signal: AbortSignal,
): Promise<void> {
  await apiClient.request(
    'post',
    flow === 'activate'
      ? '/api/v1/Auth/invitations/accept'
      : '/api/v1/Auth/password/reset',
    { body: { token, newPassword }, signal },
  );
}
