import type { QueryParameters } from '@/api/contracts';

export const userStatuses = [
  'PendingVerification',
  'Active',
  'Invited',
  'Disabled',
  'Deactivated',
] as const;
export type UserStatus = (typeof userStatuses)[number];
export type UsersParameters = {
  page: number;
  pageSize: number;
  search: string;
  status: UserStatus | undefined;
  emailVerified: boolean | undefined;
  sortBy: 'createdAt' | 'email' | 'status';
  sortDirection: 'asc' | 'desc';
};

// The backend uses an int32 SQL offset, not an unbounded page number.
export const maxUsersPage = (pageSize: number) =>
  Math.min(2_147_483_647, Math.floor(2_147_483_647 / pageSize) + 1);
const integer = (value: string | null, fallback: number, max: number) =>
  value && /^\d+$/.test(value) && Number(value) >= 1 && Number(value) <= max
    ? Number(value)
    : fallback;

export function cleanSearch(value: string): string {
  return value
    .split('')
    .filter((character) => {
      const code = character.charCodeAt(0);
      return code >= 32 && (code < 127 || code > 159);
    })
    .join('')
    .slice(0, 200)
    .trim();
}

export function parseUsersParameters(query: URLSearchParams): UsersParameters {
  const pageSize = integer(query.get('pageSize'), 25, 100);
  const status = userStatuses.find((value) => value === query.get('status'));
  const verified = query.get('emailVerified');
  const sort = query.get('sortBy');
  return {
    page: integer(query.get('page'), 1, maxUsersPage(pageSize)),
    pageSize,
    search: cleanSearch(query.get('search') ?? ''),
    status,
    emailVerified:
      verified === 'true' ? true : verified === 'false' ? false : undefined,
    sortBy: sort === 'email' || sort === 'status' ? sort : 'createdAt',
    sortDirection: query.get('sortDirection') === 'asc' ? 'asc' : 'desc',
  };
}

export function usersSearchParameters(
  values: UsersParameters,
): URLSearchParams {
  const result = new URLSearchParams();
  if (values.page !== 1) result.set('page', String(values.page));
  if (values.pageSize !== 25) result.set('pageSize', String(values.pageSize));
  if (values.search) result.set('search', values.search);
  if (values.status) result.set('status', values.status);
  if (values.emailVerified !== undefined)
    result.set('emailVerified', String(values.emailVerified));
  if (values.sortBy !== 'createdAt') result.set('sortBy', values.sortBy);
  if (values.sortDirection !== 'desc')
    result.set('sortDirection', values.sortDirection);
  return result;
}

export function usersApiQuery(
  values: UsersParameters,
): QueryParameters<'/api/v1/admin/users', 'get'> {
  return {
    Page: values.page,
    PageSize: values.pageSize,
    Search: values.search || undefined,
    Status: values.status,
    EmailVerified: values.emailVerified,
    SortBy: values.sortBy,
    SortDirection: values.sortDirection,
  };
}
