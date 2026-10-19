import {
  parseUsersParameters,
  usersSearchParameters,
} from '@/features/users/parameters';
import { isUserId } from '@/features/users/userId';
import {
  auditSearchParameters,
  parseAuditParameters,
} from '@/features/audit/parameters';

// Catalog and details accept only validated catalog filters, including after login.
export function safeReturnPath(value: unknown): string {
  if (
    typeof value === 'string' &&
    value.includes('?') &&
    !value.includes('#') &&
    !value.includes('\\')
  ) {
    const index = value.indexOf('?');
    const pathname = value.slice(0, index);
    if (pathname === '/admin/audit') {
      const query = auditSearchParameters(
        parseAuditParameters(new URLSearchParams(value.slice(index + 1))),
      ).toString();
      return pathname + (query ? `?${query}` : '');
    }
    if (
      pathname !== '/admin/users' &&
      !(
        pathname.startsWith('/admin/users/') &&
        isUserId(pathname.slice('/admin/users/'.length))
      )
    )
      return '/admin';
    const query = usersSearchParameters(
      parseUsersParameters(new URLSearchParams(value.slice(index + 1))),
    ).toString();
    return pathname + (query ? `?${query}` : '');
  }
  return typeof value === 'string' &&
    /^\/admin(?:\/[A-Za-z0-9_-]+)*\/?$/.test(value) &&
    !/^\/admin\/login(?:\/|$)/i.test(value)
    ? value
    : '/admin';
}
