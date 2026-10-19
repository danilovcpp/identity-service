import { describe, expect, expectTypeOf, it, vi } from 'vitest';
import { ApiError, ClientError } from '@/api/errors';
import { createApiClient } from '@/api/httpClient';
import type { ApiResponse } from '@/api/contracts';
import type { components } from '@/api/schema';

const profile = {
  id: 'a-user-id',
  email: 'admin@example.test',
  displayName: null,
  role: 'IdentityAdministrator',
} satisfies components['schemas']['AdminProfile'];
const login = { email: 'admin@example.test', password: 'unit-test-password' };
const csrf = () => json({ token: 'test-csrf-token' });

function json(data: unknown, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

function setup() {
  const fetch = vi.fn<typeof globalThis.fetch>();
  return { fetch, client: createApiClient(fetch) };
}

function deferredResponse() {
  let resolve!: (response: Response) => void;
  const promise = new Promise<Response>((done) => {
    resolve = done;
  });
  return { promise, resolve };
}

describe('typed API client', () => {
  it.each(['network', 'unauthorized', 'csrf'])(
    'never retries DELETE after %s failure',
    async (failure) => {
      const { fetch, client } = setup();
      fetch.mockResolvedValueOnce(csrf());
      if (failure === 'network')
        fetch.mockRejectedValueOnce(new TypeError('transport failure'));
      else
        fetch.mockResolvedValueOnce(
          json(
            { code: failure === 'csrf' ? 'csrf_invalid' : 'unauthorized' },
            failure === 'csrf' ? 400 : 401,
          ),
        );
      await expect(
        client.request(
          'delete',
          '/api/v1/admin/users/{userId}/sessions/{sessionId}',
          {
            path: { userId: 'u', sessionId: 's' },
          },
        ),
      ).rejects.toBeInstanceOf(Error);
      expect(fetch).toHaveBeenCalledTimes(2);
    },
  );

  it('explicit invalidation cancels a pending acquisition and never caches its late response', async () => {
    const { fetch, client } = setup();
    const deferred = deferredResponse();
    fetch.mockReturnValueOnce(deferred.promise);
    const pending = client.prepareCsrf();
    client.clearCsrfToken();
    deferred.resolve(csrf());
    await expect(pending).rejects.toMatchObject({ kind: 'aborted' });
    fetch
      .mockResolvedValueOnce(json({ token: 'fresh-test-csrf' }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    await client.request('post', '/api/v1/admin/users/{userId}/enable', {
      path: { userId: 'u' },
    });
    expect(fetch).toHaveBeenCalledTimes(3);
    expect(
      new Headers(fetch.mock.calls[2]?.[1]?.headers).get('X-CSRF-TOKEN'),
    ).toBe('fresh-test-csrf');
  });

  it('GET uses same-origin JSON, no-store and no redirects, without fetching CSRF', async () => {
    const { fetch, client } = setup();
    fetch.mockResolvedValue(json(profile));
    const result = await client.request('get', '/api/v1/admin/auth/me', {});
    expectTypeOf(result).toEqualTypeOf<components['schemas']['AdminProfile']>();
    expect(result).toEqual(profile);
    expect(fetch).toHaveBeenCalledExactlyOnceWith(
      '/api/v1/admin/auth/me',
      expect.objectContaining({
        method: 'GET',
        credentials: 'same-origin',
        cache: 'no-store',
        redirect: 'error',
        headers: { Accept: 'application/json' },
      }),
    );
  });

  it('infers success DTOs and rejects invalid contracts at compile time', () => {
    expectTypeOf<
      ApiResponse<'/api/v1/admin/users/{userId}', 'get'>
    >().toEqualTypeOf<components['schemas']['AdminUserDetails']>();
    expectTypeOf<
      ApiResponse<'/api/v1/admin/users/{userId}/sessions', 'get'>
    >().toEqualTypeOf<components['schemas']['PageResultOfAdminUserSession']>();
    expectTypeOf<
      ApiResponse<'/api/v1/admin/users/invitations', 'post'>
    >().toEqualTypeOf<void>();
    const compileOnly = () => {
      const { client } = setup();
      // @ts-expect-error Unknown route.
      void client.request('get', '/api/v1/admin/made-up', {});
      // @ts-expect-error Method not in the backend contract.
      void client.request('delete', '/api/v1/admin/dashboard', {});
      // @ts-expect-error Required path parameters are missing.
      void client.request('get', '/api/v1/admin/users/{userId}', {});
      void client.request('post', '/api/v1/admin/auth/login', {
        // @ts-expect-error Request fields come from OpenAPI.
        body: { invented: true },
      });
      // @ts-expect-error Native JWT login is not browser-console authentication.
      void client.request('post', '/api/v1/Auth/login', { body: login });
      // @ts-expect-error CSRF token must not be put in the query cache via request().
      void client.request('get', '/api/v1/admin/auth/csrf', {});
    };
    expectTypeOf(compileOnly).toBeFunction();
  });

  it('serializes server query parameters including false, Unicode and null omission', async () => {
    const { fetch, client } = setup();
    fetch.mockResolvedValue(
      json({ items: [], page: 1, pageSize: 25, total: 0 }),
    );
    await client.request('get', '/api/v1/admin/users', {
      query: {
        Page: 2,
        Search: 'É + &',
        EmailVerified: false,
        Status: undefined,
      },
    });
    expect(fetch.mock.calls[0]?.[0]).toBe(
      '/api/v1/admin/users?Page=2&Search=%C3%89+%2B+%26&EmailVerified=false',
    );
  });

  it('POST fetches the actual {token} contract and sends only a JSON body and CSRF header', async () => {
    const { fetch, client } = setup();
    fetch.mockResolvedValueOnce(csrf()).mockResolvedValueOnce(json(profile));
    await client.request('post', '/api/v1/admin/auth/login', { body: login });
    expect(fetch).toHaveBeenCalledTimes(2);
    expect(fetch.mock.calls[0]?.[0]).toBe('/api/v1/admin/auth/csrf');
    expect(fetch.mock.calls[1]).toEqual([
      '/api/v1/admin/auth/login',
      expect.objectContaining({
        method: 'POST',
        credentials: 'same-origin',
        body: JSON.stringify(login),
        headers: {
          Accept: 'application/json',
          'Content-Type': 'application/json',
          'X-CSRF-TOKEN': 'test-csrf-token',
        },
      }),
    ]);
  });

  it('shares a prepared token for ordinary mutations and supports DELETE path parameters', async () => {
    const { fetch, client } = setup();
    fetch
      .mockResolvedValueOnce(csrf())
      .mockImplementation(() =>
        Promise.resolve(new Response(null, { status: 204 })),
      );
    await expect(client.prepareCsrf()).resolves.toBeUndefined();
    await client.request('post', '/api/v1/admin/users/{userId}/enable', {
      path: { userId: 'first' },
    });
    await client.request(
      'delete',
      '/api/v1/admin/users/{userId}/sessions/{sessionId}',
      {
        path: { userId: 'first', sessionId: 'session/with?#characters' },
      },
    );
    expect(fetch).toHaveBeenCalledTimes(3);
    expect(fetch.mock.calls[2]).toEqual([
      '/api/v1/admin/users/first/sessions/session%2Fwith%3F%23characters',
      expect.objectContaining({
        method: 'DELETE',
        headers: {
          Accept: 'application/json',
          'X-CSRF-TOKEN': 'test-csrf-token',
        },
      }),
    ]);
  });

  it('handles empty 204 without reading JSON or a body, and empty 202 without parsing JSON', async () => {
    const { fetch, client } = setup();
    const noContent = new Response(null, { status: 204 });
    const accepted = new Response(null, { status: 202 });
    const text = vi.spyOn(noContent, 'text');
    const parse = vi.spyOn(accepted, 'json');
    fetch
      .mockResolvedValueOnce(csrf())
      .mockResolvedValueOnce(noContent)
      .mockResolvedValueOnce(accepted);
    await expect(
      client.request('post', '/api/v1/admin/users/{userId}/enable', {
        path: { userId: 'user' },
      }),
    ).resolves.toBeUndefined();
    await expect(
      client.request('post', '/api/v1/admin/users/invitations', {
        body: { email: 'new@example.test' },
      }),
    ).resolves.toBeUndefined();
    expect(text).not.toHaveBeenCalled();
    expect(parse).not.toHaveBeenCalled();
  });

  it.each([400, 401, 403, 404, 409, 429, 500, 503])(
    'normalizes HTTP %i without replaying the mutation',
    async (status) => {
      const { fetch, client } = setup();
      fetch.mockResolvedValueOnce(csrf()).mockResolvedValueOnce(
        json(
          {
            status: 999,
            code: 'documented_error',
            traceId: 'trace-123',
            errors: { email: ['Invalid email'], broken: [false] },
            detail: 'do-not-retain',
            instance: '/secret-link',
            metadata: { token: 'do-not-retain' },
          },
          status,
        ),
      );
      await expect(
        client.request('post', '/api/v1/admin/auth/login', { body: login }),
      ).rejects.toMatchObject({
        kind: 'http',
        status,
        code: 'documented_error',
        traceId: 'trace-123',
        fieldErrors: { email: ['Invalid email'] },
      });
      expect(fetch).toHaveBeenCalledTimes(2);
      const error = new ApiError(status, {
        detail: 'do-not-retain',
        token: 'do-not-retain',
      });
      expect(JSON.stringify(error)).not.toContain('do-not-retain');
      expect(error.message).not.toContain('do-not-retain');
    },
  );

  it('does not retry a CSRF failure, and obtains a new token only for the next explicit command', async () => {
    const { fetch, client } = setup();
    fetch
      .mockResolvedValueOnce(csrf())
      .mockResolvedValueOnce(json({ code: 'csrf_invalid' }, 400))
      .mockResolvedValueOnce(csrf())
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    await expect(
      client.request(
        'delete',
        '/api/v1/admin/users/{userId}/sessions/{sessionId}',
        {
          path: { userId: 'u', sessionId: 's' },
        },
      ),
    ).rejects.toMatchObject({ status: 400, code: 'csrf_invalid' });
    expect(fetch).toHaveBeenCalledTimes(2);
    await client.request('post', '/api/v1/admin/users/{userId}/enable', {
      path: { userId: 'u' },
    });
    expect(fetch).toHaveBeenCalledTimes(4);
  });

  it('clears identity-bound CSRF after login/logout and after 401 without a login loop', async () => {
    const { fetch, client } = setup();
    fetch
      .mockResolvedValueOnce(csrf())
      .mockResolvedValueOnce(json(profile))
      .mockResolvedValueOnce(csrf())
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(csrf())
      .mockResolvedValueOnce(json({ code: 'unauthorized' }, 401))
      .mockResolvedValueOnce(csrf());
    await client.request('post', '/api/v1/admin/auth/login', { body: login });
    await client.request('post', '/api/v1/admin/auth/logout', {});
    await expect(
      client.request('post', '/api/v1/admin/users/{userId}/enable', {
        path: { userId: 'u' },
      }),
    ).rejects.toMatchObject({ status: 401 });
    expect(fetch).toHaveBeenCalledTimes(6);
    await client.prepareCsrf();
    expect(fetch).toHaveBeenCalledTimes(7);
  });

  it('distinguishes network errors, discards their original details and never replays POST', async () => {
    const { fetch, client } = setup();
    fetch
      .mockResolvedValueOnce(csrf())
      .mockRejectedValueOnce(new TypeError('secret request URL'));
    await expect(
      client.request('post', '/api/v1/admin/auth/login', { body: login }),
    ).rejects.toMatchObject({ name: 'ClientError', kind: 'network' });
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('does not send the command when CSRF acquisition fails', async () => {
    const { fetch, client } = setup();
    fetch.mockResolvedValueOnce(json({ code: 'admin_unavailable' }, 503));
    await expect(
      client.request('post', '/api/v1/admin/auth/logout', {}),
    ).rejects.toMatchObject({ status: 503 });
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it.each([null, {}, { token: '' }, { token: 1 }, { token: 'bad\nheader' }])(
    'rejects malformed CSRF data %j without submitting credentials',
    async (value) => {
      const { fetch, client } = setup();
      fetch.mockResolvedValue(json(value));
      await expect(
        client.request('post', '/api/v1/admin/auth/login', { body: login }),
      ).rejects.toMatchObject({ kind: 'invalid_response' });
      expect(fetch).toHaveBeenCalledTimes(1);
    },
  );

  it('passes AbortSignal to GET and does no I/O for an already cancelled request', async () => {
    const { fetch, client } = setup();
    const controller = new AbortController();
    fetch.mockResolvedValue(json(profile));
    await client.request('get', '/api/v1/admin/auth/me', {
      signal: controller.signal,
    });
    expect(fetch.mock.calls[0]?.[1]?.signal).toBe(controller.signal);
    controller.abort('private caller reason');
    await expect(
      client.request('post', '/api/v1/admin/auth/login', {
        body: login,
        signal: controller.signal,
      }),
    ).rejects.toMatchObject({ kind: 'aborted' });
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it('coalesces concurrent CSRF acquisition and isolates caller cancellation', async () => {
    const { fetch, client } = setup();
    const deferred = deferredResponse();
    const controller = new AbortController();
    fetch
      .mockReturnValueOnce(deferred.promise)
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    const cancelled = client.request('post', '/api/v1/admin/auth/logout', {
      signal: controller.signal,
    });
    const other = client.request(
      'post',
      '/api/v1/admin/users/{userId}/enable',
      { path: { userId: 'u' } },
    );
    controller.abort();
    await expect(cancelled).rejects.toMatchObject({ kind: 'aborted' });
    expect(fetch.mock.calls[0]?.[1]?.signal?.aborted).toBe(false);
    deferred.resolve(csrf());
    await expect(other).resolves.toBeUndefined();
    expect(fetch).toHaveBeenCalledTimes(2);
    expect(fetch.mock.calls[1]?.[0]).toBe('/api/v1/admin/users/u/enable');
  });

  it('cancels the underlying CSRF request when its last caller cancels', async () => {
    const { fetch, client } = setup();
    const deferred = deferredResponse();
    const controller = new AbortController();
    fetch.mockReturnValueOnce(deferred.promise);
    const pending = client.prepareCsrf({ signal: controller.signal });
    controller.abort();
    await expect(pending).rejects.toMatchObject({ kind: 'aborted' });
    expect(fetch.mock.calls[0]?.[1]?.signal?.aborted).toBe(true);
    deferred.resolve(csrf());
    await Promise.resolve();
    fetch.mockResolvedValueOnce(csrf());
    await client.prepareCsrf();
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('recognizes a caller-provided timeout during fetch without retaining its reason', async () => {
    const { fetch, client } = setup();
    const controller = new AbortController();
    fetch.mockImplementationOnce(
      (_input, init) =>
        new Promise((_resolve, reject) => {
          init?.signal?.addEventListener(
            'abort',
            () => reject(new Error('private transport details')),
            { once: true },
          );
        }),
    );
    const pending = client.request('get', '/api/v1/admin/auth/me', {
      signal: controller.signal,
    });
    controller.abort(
      new DOMException('private timeout reason', 'TimeoutError'),
    );
    await expect(pending).rejects.toMatchObject({ kind: 'timeout' });
  });

  it.each([
    new Response('<html>proxy</html>', { status: 200 }),
    new Response('{', { headers: { 'Content-Type': 'application/json' } }),
    new Response(null),
  ])('rejects unexpected success responses', async (response) => {
    const { fetch, client } = setup();
    fetch.mockResolvedValue(response);
    await expect(
      client.request('get', '/api/v1/admin/auth/me', {}),
    ).rejects.toBeInstanceOf(ClientError);
  });

  it('preserves HTTP status even when an error body is HTML or malformed JSON', async () => {
    const { fetch, client } = setup();
    fetch
      .mockResolvedValueOnce(new Response('<html>no</html>', { status: 403 }))
      .mockResolvedValueOnce(
        new Response('{', {
          status: 401,
          headers: { 'Content-Type': 'application/problem+json' },
        }),
      );
    await expect(
      client.request('get', '/api/v1/admin/auth/me', {}),
    ).rejects.toMatchObject({ status: 403, code: 'http_403' });
    await expect(
      client.request('get', '/api/v1/admin/auth/me', {}),
    ).rejects.toMatchObject({ status: 401, code: 'http_401' });
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('public account flows use JSON without acquiring admin CSRF', async () => {
    const { fetch, client } = setup();
    fetch.mockImplementation(() =>
      Promise.resolve(new Response(null, { status: 204 })),
    );
    await client.request('post', '/api/v1/Auth/invitations/accept', {
      body: { token: 'test-invitation', newPassword: 'test-password' },
    });
    await client.request('post', '/api/v1/Auth/password/reset', {
      body: { token: 'test-reset', newPassword: 'test-password' },
    });
    expect(fetch).toHaveBeenCalledTimes(2);
    expect(
      fetch.mock.calls.every(
        ([, init]) => !new Headers(init?.headers).has('X-CSRF-TOKEN'),
      ),
    ).toBe(true);
  });

  it('keeps tokens out of storage and logs, and does not share memory between clients', async () => {
    const storage = vi.spyOn(Storage.prototype, 'setItem');
    const log = vi.spyOn(console, 'log');
    const error = vi.spyOn(console, 'error');
    const warn = vi.spyOn(console, 'warn');
    const fetch = vi
      .fn<typeof globalThis.fetch>()
      .mockImplementation(() => Promise.resolve(csrf()));
    const first = createApiClient(fetch);
    const second = createApiClient(fetch);
    await first.prepareCsrf();
    await second.prepareCsrf();
    await first.prepareCsrf();
    expect(fetch).toHaveBeenCalledTimes(2);
    first.clearCsrfToken();
    await first.prepareCsrf();
    expect(fetch).toHaveBeenCalledTimes(3);
    expect(storage).not.toHaveBeenCalled();
    expect(log).not.toHaveBeenCalled();
    expect(error).not.toHaveBeenCalled();
    expect(warn).not.toHaveBeenCalled();
    expect(document.cookie).not.toContain('test-csrf-token');
  });

  it('rejects path traversal before performing any HTTP request', async () => {
    const { fetch, client } = setup();
    await expect(
      client.request('get', '/api/v1/admin/users/{userId}', {
        path: { userId: '..' },
      }),
    ).rejects.toMatchObject({ kind: 'invalid_request' });
    expect(fetch).not.toHaveBeenCalled();
  });
});
