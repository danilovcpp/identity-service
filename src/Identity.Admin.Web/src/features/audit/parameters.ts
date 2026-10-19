import type { QueryParameters } from '@/api/contracts';
import { isUserId } from '@/features/users/userId';

export type AuditParameters = {
  page: number;
  pageSize: number;
  actorUserId: string;
  userId: string;
  eventType: string;
  from: string;
  to: string;
};
export const maxAuditPage = (size: number) =>
  Math.min(2_147_483_647, Math.floor(2_147_483_647 / size) + 1);
export const isEventType = (value: string) => /^[a-z_]{1,80}$/.test(value);
// Preserve .NET's sub-millisecond filter boundaries when restoring an existing URL.
export function compareAuditInstants(left: string, right: string): number {
  const ticks = (value: string) =>
    BigInt(Date.parse(value)) * 10_000n +
    BigInt((/\.(\d+)/.exec(value)?.[1] ?? '').slice(3).padEnd(4, '0'));
  return ticks(left) < ticks(right) ? -1 : ticks(left) > ticks(right) ? 1 : 0;
}
const integer = (value: string | null, fallback: number, max: number) =>
  value && /^\d+$/.test(value) && Number(value) >= 1 && Number(value) <= max
    ? Number(value)
    : fallback;
// Match the API's explicit-offset ISO contract, without silently rolling Feb 30 over.
export function isAuditInstant(value: string): boolean {
  const match =
    /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.\d{1,7})?(Z|[+-]\d{2}:\d{2})$/.exec(
      value,
    );
  if (!match) return false;
  const [year, month, day, hour, minute, second] = match
    .slice(1, 7)
    .map(Number);
  if (
    year === undefined ||
    month === undefined ||
    day === undefined ||
    hour === undefined ||
    minute === undefined ||
    second === undefined
  )
    return false;
  const calendar = new Date(0);
  calendar.setUTCFullYear(year, month - 1, day);
  calendar.setUTCHours(hour, minute, second, 0);
  const offset = match[7];
  if (offset !== 'Z' && offset) {
    const hours = Number(offset.slice(1, 3));
    const minutes = Number(offset.slice(4, 6));
    if (hours > 14 || minutes > 59 || (hours === 14 && minutes !== 0))
      return false;
  }
  return (
    year >= 1 &&
    calendar.getUTCFullYear() === year &&
    calendar.getUTCMonth() === month - 1 &&
    calendar.getUTCDate() === day &&
    calendar.getUTCHours() === hour &&
    calendar.getUTCMinutes() === minute &&
    calendar.getUTCSeconds() === second &&
    Number.isFinite(Date.parse(value))
  );
}
export function parseAuditParameters(query: URLSearchParams): AuditParameters {
  const pageSize = integer(query.get('pageSize'), 25, 100);
  const id = (key: string) => {
    const value = query.get(key)?.trim();
    return isUserId(value) ? value.toLowerCase() : '';
  };
  const eventType = query.get('eventType')?.trim() ?? '';
  let from = query.get('from') ?? '';
  let to = query.get('to') ?? '';
  if (!isAuditInstant(from)) from = '';
  if (!isAuditInstant(to)) to = '';
  if (from && to && compareAuditInstants(from, to) >= 0) {
    from = '';
    to = '';
  }
  return {
    page: integer(query.get('page'), 1, maxAuditPage(pageSize)),
    pageSize,
    actorUserId: id('actorUserId'),
    userId: id('userId'),
    eventType: isEventType(eventType) ? eventType : '',
    from,
    to,
  };
}
export function auditSearchParameters(
  values: AuditParameters,
): URLSearchParams {
  const query = new URLSearchParams();
  if (values.page !== 1) query.set('page', String(values.page));
  if (values.pageSize !== 25) query.set('pageSize', String(values.pageSize));
  for (const key of [
    'actorUserId',
    'userId',
    'eventType',
    'from',
    'to',
  ] as const)
    if (values[key]) query.set(key, values[key]);
  return query;
}
export function auditApiQuery(
  values: AuditParameters,
): QueryParameters<'/api/v1/admin/audit-events', 'get'> {
  return {
    Page: values.page,
    PageSize: values.pageSize,
    ActorUserId: values.actorUserId || undefined,
    UserId: values.userId || undefined,
    EventType: values.eventType || undefined,
    From: values.from || undefined,
    To: values.to || undefined,
  };
}
