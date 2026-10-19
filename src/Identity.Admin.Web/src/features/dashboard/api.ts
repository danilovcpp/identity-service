import type { ApiResponse } from '@/api/contracts';
import { ClientError } from '@/api/errors';
import { apiClient } from '@/api/httpClient';

type DashboardDto = ApiResponse<'/api/v1/admin/dashboard', 'get'>;
export type Metric = keyof Pick<
  DashboardDto,
  | 'totalUsers'
  | 'activeUsers'
  | 'activeSessions'
  | 'pendingVerificationUsers'
  | 'invitedUsers'
  | 'disabledUsers'
  | 'deactivatedUsers'
>;
export type Dashboard = Omit<DashboardDto, Metric | 'registrationTrend'> &
  Record<Metric, number> & {
    registrationTrend: { date: string; count: number }[];
  };

function count(value: number | string): number {
  if (typeof value !== 'number' && typeof value !== 'string')
    throw new ClientError('invalid_response');
  if (typeof value === 'string' && !/^\d+$/.test(value))
    throw new ClientError('invalid_response');
  const result = Number(value);
  if (!Number.isSafeInteger(result) || result < 0)
    throw new ClientError('invalid_response');
  return result;
}

export async function getDashboard(signal: AbortSignal): Promise<Dashboard> {
  const data = await apiClient.request('get', '/api/v1/admin/dashboard', {
    signal,
  });
  const metrics = {
    totalUsers: count(data.totalUsers),
    activeUsers: count(data.activeUsers),
    activeSessions: count(data.activeSessions),
    pendingVerificationUsers: count(data.pendingVerificationUsers),
    invitedUsers: count(data.invitedUsers),
    disabledUsers: count(data.disabledUsers),
    deactivatedUsers: count(data.deactivatedUsers),
  };
  for (const timestamp of [
    data.asOf,
    data.registrationTrendFrom,
    data.registrationTrendTo,
  ]) {
    if (
      typeof timestamp !== 'string' ||
      !Number.isFinite(Date.parse(timestamp))
    )
      throw new ClientError('invalid_response');
  }
  if (!Array.isArray(data.registrationTrend))
    throw new ClientError('invalid_response');
  const registrationTrend = data.registrationTrend.map((point) => {
    if (
      typeof point.date !== 'string' ||
      !/^\d{4}-\d{2}-\d{2}$/.test(point.date) ||
      !Number.isFinite(Date.parse(point.date)) ||
      new Date(point.date).toISOString().slice(0, 10) !== point.date
    )
      throw new ClientError('invalid_response');
    return { date: point.date, count: count(point.count) };
  });
  // An empty series is explicit absence of trend data, never a synthesized zero series.
  return {
    ...metrics,
    registrationTrend,
    asOf: data.asOf,
    registrationTrendFrom: data.registrationTrendFrom,
    registrationTrendTo: data.registrationTrendTo,
  };
}
