import {
  act,
  fireEvent,
  render,
  renderHook,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import { App, ConfigProvider } from 'antd';
import { QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '@/app/queryClient';
import { apiClient } from '@/api/httpClient';
import { ClientError } from '@/api/errors';
import { queryKeys } from '@/api/queryKeys';
import { TestAuth } from '@/test/TestAuth';
import { testUserDetails as account } from '@/test/userDetails';
import { testSession as session } from '@/test/userSessions';
import { getUserSessions, sessionsQuery, type UserSession } from './api';
import { UserSessions } from './UserSessions';
import { useRevokeSessions } from './useRevokeSessions';

const id = account.id;
const json = (value: unknown, status = 200) =>
  new Response(JSON.stringify(value), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
const url = (input: RequestInfo | URL) =>
  typeof input === 'string'
    ? input
    : input instanceof URL
      ? input.href
      : input.url;
const data = (
  items: unknown[] = [session],
  page = 1,
  total = items.length,
) => ({ items, page, pageSize: 25, total });
function responses(
  items: UserSession[] = [session],
  command: () => Response | Promise<Response> = () =>
    new Response(null, { status: 204 }),
) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation((input, options) => {
    const path = url(input);
    if (path.endsWith('/csrf'))
      return Promise.resolve(json({ token: 'test-sessions-csrf' }));
    if (options?.method === 'GET') return Promise.resolve(json(data(items)));
    return Promise.resolve().then(command);
  });
}
function mount(canWrite = true) {
  const client = createQueryClient();
  const affected = [
    queryKeys.users.detail(id),
    queryKeys.users.sessions(id, { page: 2 }),
    queryKeys.dashboard(),
    queryKeys.audit.list({ UserId: id }),
  ];
  const unaffected = [queryKeys.auth.me(), queryKeys.users.detail(session.id)];
  for (const key of [...affected, ...unaffected])
    client.setQueryData(key, { testData: true });
  const view = render(
    <QueryClientProvider client={client}>
      <ConfigProvider theme={{ token: { motion: false } }}>
        <App>
          <TestAuth canWrite={canWrite}>
            <UserSessions userId={id} email={account.email} />
          </TestAuth>
        </App>
      </ConfigProvider>
    </QueryClientProvider>,
  );
  return { ...view, client, affected, unaffected };
}
async function open(all = false) {
  fireEvent.click(
    await screen.findByRole('button', {
      name: all ? 'Revoke all sessions' : `Revoke session ${session.id}`,
    }),
  );
  const dialog = await screen.findByRole('dialog', {
    name: all ? 'Revoke all sessions?' : 'Revoke session?',
  });
  await waitFor(() => expect(dialog).toBeVisible());
  return dialog;
}
function confirm(dialog: HTMLElement, all = false) {
  fireEvent.click(
    within(dialog).getByRole('button', {
      name: all ? 'Confirm revoke all' : 'Confirm revoke session',
    }),
  );
}
beforeEach(() => {
  apiClient.clearCsrfToken();
  // jsdom has no layout; real browser checks cover resizing and overflow.
  vi.stubGlobal(
    'ResizeObserver',
    class {
      observe() {
        /* no jsdom layout */
      }
      unobserve() {
        /* no jsdom layout */
      }
      disconnect() {
        /* no jsdom layout */
      }
    },
  );
});
afterEach(() => vi.unstubAllGlobals());

describe('user session reads', () => {
  it('loads before showing data, formats UTC tooltips, and supports empty results', async () => {
    let complete: ((response: Response) => void) | undefined;
    vi.spyOn(globalThis, 'fetch').mockImplementation(
      () =>
        new Promise<Response>((resolve) => {
          complete = resolve;
        }),
    );
    mount(false);
    expect(
      screen.getByRole('status', { name: 'Loading sessions…' }),
    ).toBeVisible();
    await act(async () => {
      complete?.(json(data([])));
      await Promise.resolve();
    });
    expect(
      await screen.findByRole('heading', { name: 'No sessions found' }),
    ).toBeVisible();
    expect(
      screen.queryByRole('button', { name: /Revoke/ }),
    ).not.toBeInTheDocument();
  });
  it('shows server state, nullable values and no actions for expired/revoked sessions', async () => {
    responses([
      session,
      {
        ...session,
        id: account.id,
        isActive: false,
        absoluteExpiresAt: '2026-01-03T12:00:00Z',
      },
      {
        ...session,
        id: 'fb148904-c939-44af-90fe-e1c26b3f0f75',
        isActive: false,
        revokedAt: '2026-01-03T12:00:00Z',
        revokeReason: 'AdministratorRevoked',
        userAgent: '<script>agent</script>',
      },
    ]);
    mount();
    await screen.findByText('AdministratorRevoked');
    expect(
      screen.getByText('Active', { selector: 'span.ant-tag' }),
    ).toBeVisible();
    expect(
      screen.getByText('Expired', { selector: 'span.ant-tag' }),
    ).toBeVisible();
    expect(
      screen.getByText('Revoked', { selector: 'span.ant-tag' }),
    ).toBeVisible();
    expect(screen.getAllByText('Not revoked')).toHaveLength(2);
    expect(screen.getByText('<script>agent</script>')).toBeVisible();
    expect(document.querySelector('script')).toBeNull();
    expect(
      screen.getAllByRole('button', { name: /^Revoke session / }),
    ).toHaveLength(1);
    expect(document.querySelector('time')).toHaveAttribute(
      'title',
      '2026-01-01T12:00:00.000Z',
    );
  });
  it('ReadOnly reads active sessions without either revoke control', async () => {
    responses();
    mount(false);
    expect(await screen.findByText(session.id)).toBeVisible();
    expect(
      screen.queryByRole('button', { name: /Revoke/ }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('columnheader', { name: 'Actions' }),
    ).not.toBeInTheDocument();
  });
  it('uses server pagination and resets to page one on state filter change', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      const query = new URL(url(input), 'https://identity.test').searchParams;
      const page = Number(query.get('page'));
      return Promise.resolve(
        json(data([{ ...session, clientId: `page-${page}` }], page, 26)),
      );
    });
    mount(false);
    await screen.findByText('page-1');
    fireEvent.click(screen.getByTitle('Next Page').querySelector('button')!);
    await screen.findByText('page-2');
    fireEvent.mouseDown(
      screen.getByRole('combobox', { name: 'Session state' }),
    );
    fireEvent.click(await screen.findByRole('option', { name: 'Expired' }));
    await screen.findByText('page-1');
    expect(fetch).toHaveBeenLastCalledWith(
      `/api/v1/admin/users/${id}/sessions?page=1&pageSize=25&state=expired`,
      expect.objectContaining({ method: 'GET', credentials: 'same-origin' }),
    );
  });
  it('supports independent GET retry without retrying a command', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(json({}, 500));
    mount(false);
    await screen.findByRole('alert');
    fetch.mockImplementation(() => Promise.resolve(json(data())));
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    await screen.findByText(session.id);
    expect(
      fetch.mock.calls.every(([, options]) => options?.method === 'GET'),
    ).toBe(true);
  });
  it('projects only documented session fields and numeric strings into cache', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      json({
        ...data([
          {
            ...session,
            refreshToken: 'private-marker',
            password: 'private-marker',
            ip: 'private-marker',
          },
        ]),
        page: '1',
        pageSize: '25',
        total: '1',
      }),
    );
    const { client } = mount(false);
    await screen.findByText(session.id);
    expect(
      JSON.stringify(
        client.getQueryData(
          queryKeys.users.sessions(id, sessionsQuery(1, 'all')),
        ),
      ),
    ).not.toContain('private-marker');
    expect(document.body).not.toHaveTextContent('private-marker');
  });
  it.each([
    { ...data(), page: 2 },
    { ...data(), total: '9007199254740992' },
    data([{ ...session, createdAt: 'invalid' }]),
    data([{ ...session, id: 'not-a-uuid' }]),
    data([{ ...session, isActive: true, revokedAt: '2026-01-03T12:00:00Z' }]),
    data([session, session]),
  ])('rejects malformed session data', async (value) => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(value));
    await expect(
      getUserSessions(id, 1, 'all', new AbortController().signal),
    ).rejects.toBeInstanceOf(ClientError);
  });
  it('aborts an in-flight read on unmount', () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockReturnValue(new Promise(() => undefined));
    const view = mount(false);
    const signal = fetch.mock.calls[0]?.[1]?.signal;
    view.unmount();
    expect(signal?.aborted).toBe(true);
  });
});

describe('user session commands', () => {
  it('restores focus after success even while the session refetch remains pending', async () => {
    let reads = 0;
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation((input, options) => {
        if (url(input).endsWith('/csrf'))
          return Promise.resolve(json({ token: 'test-sessions-csrf' }));
        if (options?.method === 'GET') {
          reads++;
          return reads === 1
            ? Promise.resolve(json(data()))
            : new Promise<Response>(() => undefined);
        }
        return Promise.resolve(new Response(null, { status: 204 }));
      });
    mount();
    confirm(await open());
    await screen.findByText('Session revoked.');
    const refresh = screen.getByRole('button', { name: 'Refresh sessions' });
    await waitFor(() => expect(refresh).toHaveFocus());
    expect(reads).toBe(2);
    const calls = fetch.mock.calls.length;
    fireEvent.click(refresh);
    expect(fetch).toHaveBeenCalledTimes(calls);
  });
  it.each([false, true])(
    'only confirms after explicit submit; cancelling %s makes no unsafe request',
    async (all) => {
      const fetch = responses();
      mount();
      const dialog = await open(all);
      expect(dialog).toHaveTextContent(
        all ? 'all refresh and administrator browser sessions' : session.id,
      );
      expect(dialog).toHaveTextContent(
        'External access tokens can remain valid',
      );
      if (all) expect(dialog).toHaveTextContent('your browser session');
      fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
      await waitFor(() =>
        expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
      );
      expect(
        fetch.mock.calls.every(([, options]) => options?.method === 'GET'),
      ).toBe(true);
    },
  );
  it.each([false, true])(
    'waits for 204 and sends the exact %s route/method with CSRF and no body once',
    async (all) => {
      let complete: ((response: Response) => void) | undefined;
      const fetch = responses(
        [session],
        () =>
          new Promise<Response>((resolve) => {
            complete = resolve;
          }),
      );
      const { client, affected, unaffected } = mount();
      const dialog = await open(all);
      confirm(dialog, all);
      confirm(dialog, all);
      await waitFor(() =>
        expect(
          fetch.mock.calls.filter(([, options]) => options?.method !== 'GET'),
        ).toHaveLength(1),
      );
      expect(fetch).toHaveBeenLastCalledWith(
        `/api/v1/admin/users/${id}/sessions/${all ? 'revoke-all' : session.id}`,
        expect.objectContaining({
          method: all ? 'POST' : 'DELETE',
          body: undefined,
          credentials: 'same-origin',
          headers: expect.objectContaining({
            'X-CSRF-TOKEN': 'test-sessions-csrf',
          }) as HeadersInit,
        }),
      );
      expect(
        within(dialog).getByRole('button', { name: 'Cancel' }),
      ).toBeDisabled();
      expect(
        within(dialog).getByRole('button', {
          name: all ? 'Confirm revoke all' : 'Confirm revoke session',
        }),
      ).toBeDisabled();
      expect(
        screen.queryByText(all ? 'All sessions revoked.' : 'Session revoked.'),
      ).not.toBeInTheDocument();
      await act(async () => {
        complete?.(new Response(null, { status: 204 }));
        await Promise.resolve();
      });
      expect(
        await screen.findByText(
          all ? 'All sessions revoked.' : 'Session revoked.',
        ),
      ).toBeVisible();
      expect(
        affected.every(
          (key) => client.getQueryState(key)?.isInvalidated === true,
        ),
      ).toBe(true);
      expect(
        unaffected.every(
          (key) => client.getQueryState(key)?.isInvalidated === false,
        ),
      ).toBe(true);
    },
  );
  it.each([
    [403, 'forbidden', 'You do not have permission'],
    [404, 'session_not_found', 'The account or session is no longer available'],
    [409, 'admin_user_conflict', 'The session or account state has changed'],
    [400, 'csrf_invalid', 'Your security check expired'],
  ])(
    'handles %i safely without replay or invalidation',
    async (status, code, message) => {
      const fetch = responses([session], () =>
        json({ code, detail: 'private-marker' }, status),
      );
      const { client, affected } = mount();
      const dialog = await open();
      confirm(dialog);
      expect(await screen.findByText(new RegExp(message))).toBeVisible();
      expect(dialog).toBeVisible();
      expect(document.body).not.toHaveTextContent('private-marker');
      expect(
        fetch.mock.calls.filter(([, options]) => options?.method === 'DELETE'),
      ).toHaveLength(1);
      expect(
        affected.every(
          (key) => client.getQueryState(key)?.isInvalidated === false,
        ),
      ).toBe(true);
    },
  );
  it('does not replay or claim success after an uncertain network failure', async () => {
    const fetch = responses([session], () =>
      Promise.reject(new TypeError('private-marker')),
    );
    mount();
    confirm(await open(true), true);
    expect(
      await screen.findByText(/The result could not be confirmed/),
    ).toBeVisible();
    expect(screen.queryByText('All sessions revoked.')).not.toBeInTheDocument();
    expect(
      fetch.mock.calls.filter(([, options]) => options?.method === 'POST'),
    ).toHaveLength(1);
  });
  it('aborts an immediately unmounted command before HTTP', async () => {
    const fetch = responses();
    const client = createQueryClient();
    const { result, unmount } = renderHook(useRevokeSessions, {
      wrapper: ({ children }) => (
        <QueryClientProvider client={client}>{children}</QueryClientProvider>
      ),
    });
    const operation = result.current
      .execute({ kind: 'all', userId: id })
      .catch((error: unknown) => error);
    unmount();
    await expect(operation).resolves.toBeInstanceOf(ClientError);
    expect(fetch).not.toHaveBeenCalled();
  });
});
