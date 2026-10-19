import type { ApiResponse } from '@/api/contracts';
import { apiClient } from '@/api/httpClient';
import { ClientError } from '@/api/errors';
import { usersApiQuery, type UsersParameters } from './parameters';

type UsersDto = ApiResponse<'/api/v1/admin/users', 'get'>;
export type UserListItem = UsersDto['items'][number];
export type UsersPage = {
  items: UserListItem[];
  page: number;
  pageSize: number;
  total: number;
};

function integer(value: number | string): number {
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

export async function getUsers(
  parameters: UsersParameters,
  signal: AbortSignal,
): Promise<UsersPage> {
  const data = await apiClient.request('get', '/api/v1/admin/users', {
    query: usersApiQuery(parameters),
    signal,
  });
  const page = integer(data.page);
  const pageSize = integer(data.pageSize);
  const total = integer(data.total);
  if (
    page !== parameters.page ||
    pageSize !== parameters.pageSize ||
    !Array.isArray(data.items) ||
    data.items.length > pageSize
  )
    throw new ClientError('invalid_response');
  const items = data.items.map((item) => {
    if (
      !item ||
      typeof item.id !== 'string' ||
      !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
        item.id,
      ) ||
      typeof item.email !== 'string' ||
      (item.displayName !== null && typeof item.displayName !== 'string') ||
      typeof item.status !== 'string' ||
      typeof item.emailVerified !== 'boolean' ||
      typeof item.createdAt !== 'string' ||
      !Number.isFinite(Date.parse(item.createdAt))
    )
      throw new ClientError('invalid_response');
    // Cache only documented fields, never unexpected backend properties.
    return {
      id: item.id,
      email: item.email,
      displayName: item.displayName,
      status: item.status,
      emailVerified: item.emailVerified,
      createdAt: item.createdAt,
    };
  });
  return { items, page, pageSize, total };
}
