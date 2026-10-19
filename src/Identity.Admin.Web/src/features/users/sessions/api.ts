import type { ApiResponse, QueryParameters } from '@/api/contracts';
import { apiClient } from '@/api/httpClient';
import { ClientError } from '@/api/errors';
import { isUserId } from '../userId';

type SessionsDto = ApiResponse<'/api/v1/admin/users/{userId}/sessions', 'get'>;
export type UserSession = SessionsDto['items'][number];
export type SessionState = 'all' | 'active' | 'revoked' | 'expired';
export type SessionsPage = {
  items: UserSession[];
  page: number;
  pageSize: number;
  total: number;
};
export const pageSize = 25;
export const maxPage = Math.floor(2_147_483_647 / pageSize) + 1;
export function sessionsQuery(
  page: number,
  state: SessionState,
): QueryParameters<'/api/v1/admin/users/{userId}/sessions', 'get'> {
  return { page, pageSize, state: state === 'all' ? undefined : state };
}
function integer(value: unknown): number {
  if (
    (typeof value !== 'number' && typeof value !== 'string') ||
    (typeof value === 'string' && !/^\d+$/.test(value))
  )
    throw new ClientError('invalid_response');
  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed) || parsed < 0)
    throw new ClientError('invalid_response');
  return parsed;
}
const timestamp = (value: unknown): value is string =>
  typeof value === 'string' && Number.isFinite(Date.parse(value));

export async function getUserSessions(
  userId: string,
  page: number,
  state: SessionState,
  signal: AbortSignal,
): Promise<SessionsPage> {
  const data = await apiClient.request(
    'get',
    '/api/v1/admin/users/{userId}/sessions',
    {
      path: { userId },
      query: sessionsQuery(page, state),
      signal,
    },
  );
  if (
    !data ||
    integer(data.page) !== page ||
    integer(data.pageSize) !== pageSize ||
    !Array.isArray(data.items) ||
    data.items.length > pageSize
  )
    throw new ClientError('invalid_response');
  const total = integer(data.total);
  const ids = new Set<string>();
  const items = data.items.map((item) => {
    if (
      !item ||
      !isUserId(item.id) ||
      ids.has(item.id.toLowerCase()) ||
      typeof item.clientId !== 'string' ||
      (item.userAgent !== null && typeof item.userAgent !== 'string') ||
      !timestamp(item.createdAt) ||
      !timestamp(item.lastUsedAt) ||
      !timestamp(item.absoluteExpiresAt) ||
      (item.revokedAt !== null && !timestamp(item.revokedAt)) ||
      (item.revokeReason !== null && typeof item.revokeReason !== 'string') ||
      typeof item.isActive !== 'boolean' ||
      (item.isActive && item.revokedAt !== null)
    )
      throw new ClientError('invalid_response');
    ids.add(item.id.toLowerCase());
    // Explicit projection keeps unexpected credentials/token fields out of the cache.
    return {
      id: item.id,
      clientId: item.clientId,
      userAgent: item.userAgent,
      createdAt: item.createdAt,
      lastUsedAt: item.lastUsedAt,
      absoluteExpiresAt: item.absoluteExpiresAt,
      revokedAt: item.revokedAt,
      revokeReason: item.revokeReason,
      isActive: item.isActive,
    };
  });
  return { items, page, pageSize, total };
}

export type RevokeCommand =
  | { kind: 'one'; userId: string; sessionId: string }
  | { kind: 'all'; userId: string };
export function revokeSessions(command: RevokeCommand, signal: AbortSignal) {
  return command.kind === 'one'
    ? apiClient.request(
        'delete',
        '/api/v1/admin/users/{userId}/sessions/{sessionId}',
        {
          path: { userId: command.userId, sessionId: command.sessionId },
          signal,
        },
      )
    : apiClient.request(
        'post',
        '/api/v1/admin/users/{userId}/sessions/revoke-all',
        {
          path: { userId: command.userId },
          signal,
        },
      );
}
