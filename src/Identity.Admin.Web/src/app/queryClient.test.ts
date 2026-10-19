import { onlineManager } from '@tanstack/react-query';
import { describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '@/app/queryClient';
import { queryKeys } from '@/api/queryKeys';
import { ApiError } from '@/api/errors';

describe('query infrastructure', () => {
  it('creates isolated caches and never shares the query client across tests or roots', () => {
    const first = createQueryClient();
    const second = createQueryClient();
    try {
      first.setQueryData(queryKeys.auth.me(), { id: 'first' });
      expect(second.getQueryData(queryKeys.auth.me())).toBeUndefined();
      expect(first).not.toBe(second);
    } finally {
      first.clear();
      second.clear();
    }
  });

  it.each([401, 403, 409, 429, 500])(
    'does not retry a query returning HTTP %i',
    async (status) => {
      const client = createQueryClient();
      const query = vi.fn().mockRejectedValue(new ApiError(status, {}));
      try {
        await expect(
          client.fetchQuery({ queryKey: queryKeys.auth.me(), queryFn: query }),
        ).rejects.toBeInstanceOf(ApiError);
        expect(query).toHaveBeenCalledTimes(1);
      } finally {
        client.clear();
      }
    },
  );

  it('executes mutations once even while offline instead of queueing a later command', async () => {
    const client = createQueryClient();
    const execute = vi.fn().mockRejectedValue(new ApiError(401, {}));
    const previouslyOnline = onlineManager.isOnline();
    try {
      onlineManager.setOnline(false);
      const mutation = client
        .getMutationCache()
        .build(client, { mutationFn: execute });
      await expect(mutation.execute(undefined)).rejects.toMatchObject({
        status: 401,
      });
      expect(execute).toHaveBeenCalledTimes(1);
      expect(mutation.state.isPaused).toBe(false);
      expect(client.getDefaultOptions().mutations).toMatchObject({
        retry: false,
        gcTime: 0,
      });
    } finally {
      client.clear();
      onlineManager.setOnline(previouslyOnline);
    }
  });

  it('keys include all server-side filters and support resource-prefix invalidation', () => {
    const users = {
      Page: 2,
      Search: 'name',
      EmailVerified: false,
      Status: 'Active',
      SortBy: 'email',
      SortDirection: 'asc',
      PageSize: 25,
    };
    const key = queryKeys.users.list(users);
    expect(key).toEqual(['admin', 'users', 'list', users]);
    users.Page = 3;
    expect(key[3].Page).toBe(2);
    expect(queryKeys.users.list(users)).not.toEqual(key);
    expect(queryKeys.users.detail('u')).toEqual([
      'admin',
      'users',
      'detail',
      'u',
    ]);
    expect(queryKeys.users.sessions('u', { page: 2, state: 'active' })).toEqual(
      [
        ...queryKeys.users.detail('u'),
        'sessions',
        { page: 2, state: 'active' },
      ],
    );
    expect(
      queryKeys.audit.list({
        ActorUserId: 'a',
        UserId: 'u',
        EventType: 'admin_login',
        From: '2026-01-01T00:00:00Z',
        To: '2026-02-01T00:00:00Z',
        Page: 2,
      }),
    ).not.toEqual(queryKeys.audit.list({ Page: 1 }));
  });
});
