import { apiClient } from '@/api/httpClient';
import type { components } from '@/api/schema';

type DisableInput = components['schemas']['DisableUserRequest'];
export type StatusCommand =
  | {
      action: 'disable';
      userId: string;
      reason: NonNullable<DisableInput['reason']>;
    }
  | { action: 'enable'; userId: string };

export function changeAccountStatus(
  command: StatusCommand,
  signal: AbortSignal,
) {
  return command.action === 'disable'
    ? apiClient.request('post', '/api/v1/admin/users/{userId}/disable', {
        path: { userId: command.userId },
        body: { reason: command.reason },
        signal,
      })
    : apiClient.request('post', '/api/v1/admin/users/{userId}/enable', {
        path: { userId: command.userId },
        signal,
      });
}
