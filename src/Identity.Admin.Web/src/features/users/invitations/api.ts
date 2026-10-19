import { apiClient } from '@/api/httpClient';
import type { components } from '@/api/schema';

export type InviteInput = {
  email: NonNullable<components['schemas']['InviteUserRequest']['email']>;
} & Pick<components['schemas']['InviteUserRequest'], 'displayName'>;

export function inviteUser(body: InviteInput, signal: AbortSignal) {
  return apiClient.request('post', '/api/v1/admin/users/invitations', {
    body,
    signal,
  });
}

export function resendInvitation(userId: string, signal: AbortSignal) {
  return apiClient.request(
    'post',
    '/api/v1/admin/users/{userId}/invitations/resend',
    {
      path: { userId },
      signal,
    },
  );
}
