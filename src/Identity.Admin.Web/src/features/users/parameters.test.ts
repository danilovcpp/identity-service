import { describe, expect, it } from 'vitest';
import { safeReturnPath } from '@/features/auth/returnPath';
import {
  maxUsersPage,
  parseUsersParameters,
  usersApiQuery,
  usersSearchParameters,
} from './parameters';

const parse = (value: string) =>
  parseUsersParameters(new URLSearchParams(value));
describe('users URL parameters', () => {
  it('uses backend defaults and a canonical empty URL', () => {
    const values = parse('');
    expect(values).toEqual({
      page: 1,
      pageSize: 25,
      search: '',
      status: undefined,
      emailVerified: undefined,
      sortBy: 'createdAt',
      sortDirection: 'desc',
    });
    expect(usersSearchParameters(values).toString()).toBe('');
  });
  it('roundtrips every parameter and maps the exact OpenAPI query casing', () => {
    const source =
      'page=2&pageSize=50&search=Alice+%26+Bob&status=Active&emailVerified=false&sortBy=email&sortDirection=asc';
    const values = parse(source);
    expect(usersSearchParameters(values).toString()).toBe(source);
    expect(usersApiQuery(values)).toEqual({
      Page: 2,
      PageSize: 50,
      Search: 'Alice & Bob',
      Status: 'Active',
      EmailVerified: false,
      SortBy: 'email',
      SortDirection: 'asc',
    });
    expect(safeReturnPath('/admin/users?' + source + '&token=discarded')).toBe(
      '/admin/users?' + source,
    );
  });
  it.each(['-1', '0', '1.5', 'NaN', '1e3', '9007199254740993'])(
    'normalizes invalid page %s',
    (value) => {
      expect(parse('page=' + value).page).toBe(1);
    },
  );
  it('respects the int32 offset and maximum page size', () => {
    expect(parse(`page=${maxUsersPage(100)}&pageSize=100`).page).toBe(
      maxUsersPage(100),
    );
    expect(parse(`page=${maxUsersPage(100) + 1}&pageSize=100`).page).toBe(1);
    expect(parse('pageSize=101').pageSize).toBe(25);
    expect(parse('pageSize=1').pageSize).toBe(1);
    expect(parse('page=2147483648&pageSize=1').page).toBe(1);
    expect(parse('page=2147483647&pageSize=1').page).toBe(2147483647);
  });
  it('removes unknown values, control characters and excessive search length', () => {
    const values = parse(
      'sortBy=password&sortDirection=drop&status=Unknown&emailVerified=1&token=secret&search=%00%0A++hello++',
    );
    expect(usersSearchParameters(values).toString()).toBe('search=hello');
    expect(parse('search=' + 'x'.repeat(210)).search).toHaveLength(200);
  });
});
