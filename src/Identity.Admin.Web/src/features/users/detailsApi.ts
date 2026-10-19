import type { ApiResponse, QueryParameters } from '@/api/contracts';
import { apiClient } from '@/api/httpClient';
import { ClientError } from '@/api/errors';
import { isUserId } from './userId';

type DetailsDto = ApiResponse<'/api/v1/admin/users/{userId}', 'get'>;
export type UserDetails = Pick<
  DetailsDto,
  | 'id'
  | 'email'
  | 'displayName'
  | 'status'
  | 'emailVerified'
  | 'emailVerifiedAt'
  | 'createdAt'
  | 'updatedAt'
  | 'disabledAt'
  | 'deactivatedAt'
>;
type AuditDto = ApiResponse<'/api/v1/admin/audit-events', 'get'>;
export type UserActivity = Pick<
  AuditDto['items'][number],
  'id' | 'eventType' | 'occurredAt'
>;
const timestamp = (value: unknown): value is string =>
  typeof value === 'string' && Number.isFinite(Date.parse(value));
const nullableTimestamp = (value: unknown) =>
  value === null || timestamp(value);

export async function getUserDetails(
  userId: string,
  signal: AbortSignal,
): Promise<UserDetails> {
  const data = await apiClient.request('get', '/api/v1/admin/users/{userId}', {
    path: { userId },
    signal,
  });
  if (
    !data ||
    !isUserId(data.id) ||
    data.id.toLowerCase() !== userId.toLowerCase() ||
    typeof data.email !== 'string' ||
    (data.displayName !== null && typeof data.displayName !== 'string') ||
    typeof data.status !== 'string' ||
    typeof data.emailVerified !== 'boolean' ||
    !timestamp(data.createdAt) ||
    !timestamp(data.updatedAt) ||
    !nullableTimestamp(data.emailVerifiedAt) ||
    !nullableTimestamp(data.disabledAt) ||
    !nullableTimestamp(data.deactivatedAt)
  )
    throw new ClientError('invalid_response');
  return {
    id: data.id,
    email: data.email,
    displayName: data.displayName,
    status: data.status,
    emailVerified: data.emailVerified,
    emailVerifiedAt: data.emailVerifiedAt,
    createdAt: data.createdAt,
    updatedAt: data.updatedAt,
    disabledAt: data.disabledAt,
    deactivatedAt: data.deactivatedAt,
  };
}

export function userActivityQuery(
  userId: string,
): QueryParameters<'/api/v1/admin/audit-events', 'get'> {
  return { Page: 1, PageSize: 10, UserId: userId };
}

export async function getUserActivity(
  userId: string,
  signal: AbortSignal,
): Promise<UserActivity[]> {
  const data = await apiClient.request('get', '/api/v1/admin/audit-events', {
    query: userActivityQuery(userId),
    signal,
  });
  if (
    !data ||
    Number(data.page) !== 1 ||
    Number(data.pageSize) !== 10 ||
    !Array.isArray(data.items) ||
    data.items.length > 10
  )
    throw new ClientError('invalid_response');
  return data.items.map((item) => {
    const related = (value: unknown) =>
      isUserId(value) && value.toLowerCase() === userId.toLowerCase();
    if (
      !item ||
      !isUserId(item.id) ||
      typeof item.eventType !== 'string' ||
      !timestamp(item.occurredAt) ||
      (!related(item.userId) && !related(item.targetUserId))
    )
      throw new ClientError('invalid_response');
    // The card needs only the event and time. Omit actors, correlation IDs and any extra metadata.
    return {
      id: item.id,
      eventType: item.eventType,
      occurredAt: item.occurredAt,
    };
  });
}
