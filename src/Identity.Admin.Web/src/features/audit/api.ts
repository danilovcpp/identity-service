import type { ApiResponse } from '@/api/contracts';
import { apiClient } from '@/api/httpClient';
import { ClientError } from '@/api/errors';
import { isUserId } from '@/features/users/userId';
import { auditApiQuery, type AuditParameters } from './parameters';

type Dto = ApiResponse<'/api/v1/admin/audit-events', 'get'>;
export type AuditEvent = Pick<
  Dto['items'][number],
  | 'id'
  | 'userId'
  | 'actorUserId'
  | 'targetUserId'
  | 'eventType'
  | 'occurredAt'
  | 'correlationId'
>;
export type AuditPage = {
  items: AuditEvent[];
  page: number;
  pageSize: number;
  total: number;
};
function integer(value: unknown) {
  if (
    (typeof value !== 'number' && typeof value !== 'string') ||
    (typeof value === 'string' && !/^\d+$/.test(value))
  )
    throw new ClientError('invalid_response');
  const result = Number(value);
  if (!Number.isSafeInteger(result) || result < 0)
    throw new ClientError('invalid_response');
  return result;
}
export async function getAuditEvents(
  values: AuditParameters,
  signal: AbortSignal,
): Promise<AuditPage> {
  const data = await apiClient.request('get', '/api/v1/admin/audit-events', {
    query: auditApiQuery(values),
    signal,
  });
  if (
    !data ||
    integer(data.page) !== values.page ||
    integer(data.pageSize) !== values.pageSize ||
    !Array.isArray(data.items) ||
    data.items.length > values.pageSize
  )
    throw new ClientError('invalid_response');
  const total = integer(data.total);
  const ids = new Set<string>();
  const related = (value: unknown) => value === null || isUserId(value);
  const items = data.items.map((item) => {
    if (
      !item ||
      !isUserId(item.id) ||
      ids.has(item.id.toLowerCase()) ||
      !related(item.userId) ||
      !related(item.actorUserId) ||
      !related(item.targetUserId) ||
      typeof item.eventType !== 'string' ||
      typeof item.occurredAt !== 'string' ||
      !Number.isFinite(Date.parse(item.occurredAt)) ||
      typeof item.correlationId !== 'string'
    )
      throw new ClientError('invalid_response');
    ids.add(item.id.toLowerCase());
    // No metadata or unexpected response properties enter the query cache.
    return {
      id: item.id,
      userId: item.userId,
      actorUserId: item.actorUserId,
      targetUserId: item.targetUserId,
      eventType: item.eventType,
      occurredAt: item.occurredAt,
      correlationId: item.correlationId,
    };
  });
  return { items, page: values.page, pageSize: values.pageSize, total };
}
