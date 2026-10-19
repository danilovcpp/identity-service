import {
  parseUsersParameters,
  usersSearchParameters,
} from '@/features/users/parameters';

// Only the implemented users route accepts query parameters, through its allowlist.
export function safeReturnPath(value: unknown): string {
  if (
    typeof value === 'string' &&
    value.startsWith('/admin/users?') &&
    !value.includes('#') &&
    !value.includes('\\')
  ) {
    const query = usersSearchParameters(
      parseUsersParameters(
        new URLSearchParams(value.slice('/admin/users?'.length)),
      ),
    ).toString();
    return '/admin/users' + (query ? `?${query}` : '');
  }
  return typeof value === 'string' &&
    /^\/admin(?:\/[A-Za-z0-9_-]+)*\/?$/.test(value) &&
    !/^\/admin\/login(?:\/|$)/i.test(value)
    ? value
    : '/admin';
}
