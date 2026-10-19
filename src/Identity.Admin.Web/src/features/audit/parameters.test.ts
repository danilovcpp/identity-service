import { describe, expect, it } from 'vitest';
import { safeReturnPath } from '@/features/auth/returnPath';
import { testUserDetails as account } from '@/test/userDetails';
import {
  auditApiQuery,
  auditSearchParameters,
  compareAuditInstants,
  isAuditInstant,
  maxAuditPage,
  parseAuditParameters,
} from './parameters';
import { localDateInput, localInstant } from './localTime';

const parse = (value: string) =>
  parseAuditParameters(new URLSearchParams(value));
describe('audit URL filters', () => {
  it('uses server defaults and strips unknown query fields', () => {
    const values = parse('token=discarded&metadata=discarded');
    expect(values).toEqual({
      page: 1,
      pageSize: 25,
      actorUserId: '',
      userId: '',
      eventType: '',
      from: '',
      to: '',
    });
    expect(auditSearchParameters(values).toString()).toBe('');
  });
  it('roundtrips all filters, API casing and the safe login return path', () => {
    const query = new URLSearchParams({
      page: '2',
      pageSize: '50',
      actorUserId: account.id,
      userId: account.id,
      eventType: 'admin_disabled_user',
      from: '2026-01-01T00:00:00+03:00',
      to: '2026-02-01T00:00:00.1234567Z',
    });
    const values = parseAuditParameters(query);
    expect(auditSearchParameters(values).toString()).toBe(query.toString());
    expect(auditApiQuery(values)).toEqual({
      Page: 2,
      PageSize: 50,
      ActorUserId: account.id,
      UserId: account.id,
      EventType: 'admin_disabled_user',
      From: '2026-01-01T00:00:00+03:00',
      To: '2026-02-01T00:00:00.1234567Z',
    });
    expect(
      safeReturnPath('/admin/audit?' + query.toString() + '&token=discarded'),
    ).toBe('/admin/audit?' + query.toString());
    expect(safeReturnPath('//evil.test/admin/audit?' + query.toString())).toBe(
      '/admin',
    );
    expect(
      safeReturnPath('/admin/audit?eventType=register#token=discarded'),
    ).toBe('/admin');
  });
  it.each(['-1', '0', '1.2', '1e4', '9007199254740993'])(
    'normalizes invalid pages %s',
    (page) => expect(parse('page=' + page).page).toBe(1),
  );
  it('bounds pagination to the backend int32 offset and maximum size', () => {
    expect(parse(`pageSize=100&page=${maxAuditPage(100)}`).page).toBe(
      maxAuditPage(100),
    );
    expect(parse(`pageSize=100&page=${maxAuditPage(100) + 1}`).page).toBe(1);
    expect(parse('pageSize=101').pageSize).toBe(25);
    expect(parse('pageSize=1').pageSize).toBe(1);
  });
  it('rejects malformed IDs, event names, calendars and inverted periods', () => {
    const values = parse(
      'actorUserId=not-an-id&userId=00000000-0000-0000-0000-000000000000&eventType=ADMIN&from=2026-02-30T00:00:00Z&to=tomorrow',
    );
    expect(auditSearchParameters(values).toString()).toBe('');
    expect(
      parse('from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z').from,
    ).toBe('');
  });
  it.each([
    '2026-01-01',
    '2026-01-01T00:00:00',
    '2026-01-01T25:00:00Z',
    '2026-01-01T00:00:00+14:01',
    '2026-01-01T00:00:00+15:00',
    '2026-02-29T00:00:00Z',
  ])('rejects unsupported instant %s', (value) =>
    expect(isAuditInstant(value)).toBe(false),
  );
  it('keeps valid leap days, explicit offsets and sub-millisecond boundaries', () => {
    expect(isAuditInstant('2024-02-29T00:00:00Z')).toBe(true);
    expect(isAuditInstant('2026-01-01T00:00:00-14:00')).toBe(true);
    const from = '2026-01-01T00:00:00.0000001Z';
    const to = '2026-01-01T00:00:00.0000002Z';
    expect(compareAuditInstants(from, to)).toBe(-1);
    expect(parse(new URLSearchParams({ from, to }).toString()).to).toBe(to);
  });
  it('converts native local inputs to UTC and rejects rollover', () => {
    const instant = '2026-03-01T12:20:30.123Z';
    expect(localInstant(localDateInput(instant))).toBe(instant);
    expect(localInstant('2026-02-30T12:00')).toBeUndefined();
    expect(localInstant('invalid')).toBeUndefined();
  });
});
