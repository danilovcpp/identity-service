import { apiClient } from '@/api/httpClient';

export function requestPasswordReset(userId: string, signal: AbortSignal) {
  return apiClient.request(
    'post',
    '/api/v1/admin/users/{userId}/password-reset',
    { path: { userId }, signal },
  );
}
