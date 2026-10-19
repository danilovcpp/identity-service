import { QueryClientProvider } from '@tanstack/react-query';
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react';
import { ConfigProvider } from 'antd';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from '@/app/App';
import { createQueryClient } from '@/app/queryClient';
import { apiClient } from '@/api/httpClient';
import { queryKeys } from '@/api/queryKeys';
import { testProfile } from '@/test/auth';
import * as dashboardApi from '@/features/dashboard/api';
import { testDashboard } from '@/test/dashboard';
import { safeReturnPath } from './returnPath';

const json = (data: unknown, status = 200) =>
  new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
function requestPath(input: RequestInfo | URL) {
  return typeof input === 'string'
    ? input
    : input instanceof URL
      ? input.href
      : input.url;
}
function mockFetch(
  handler: (path: string, init?: RequestInit) => Response | Promise<Response>,
) {
  return vi
    .spyOn(globalThis, 'fetch')
    .mockImplementation((input, init) =>
      Promise.resolve().then(() => handler(requestPath(input), init)),
    );
}
function Path() {
  return <output aria-label="Current path">{useLocation().pathname}</output>;
}
function mount(path = '/admin', state?: unknown) {
  const client = createQueryClient();
  render(
    <QueryClientProvider client={client}>
      <ConfigProvider theme={{ token: { motion: false } }}>
        <MemoryRouter initialEntries={[{ pathname: path, state }]}>
          <App />
          <Path />
        </MemoryRouter>
      </ConfigProvider>
    </QueryClientProvider>,
  );
  return client;
}
async function fillLogin() {
  fireEvent.change(await screen.findByLabelText('Email'), {
    target: { value: testProfile.email },
  });
  fireEvent.change(screen.getByLabelText('Password'), {
    target: { value: 'Test-only password!65' },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
}

describe('administrative authentication', () => {
  beforeEach(() => {
    vi.spyOn(dashboardApi, 'getDashboard').mockResolvedValue(testDashboard);
    apiClient.clearCsrfToken();
  });

  it('shows checking state without exposing the shell', () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(
      () => new Promise<Response>(() => undefined),
    );
    mount();
    expect(
      screen.getByRole('status', { name: 'Checking your session…' }),
    ).toHaveTextContent('Checking your session');
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
  });

  it('uses CSRF, confirms /me, caches only safe profile, and clears password and cache on logout', async () => {
    let signedIn = false;
    const fetch = mockFetch((url, init) => {
      if (requestPath(url).endsWith('/csrf'))
        return json({
          token: signedIn ? 'authenticated-csrf' : 'anonymous-csrf',
        });
      if (requestPath(url).endsWith('/login')) {
        signedIn = true;
        return json(testProfile);
      }
      if (requestPath(url).endsWith('/logout')) {
        signedIn = false;
        return new Response(null, { status: 204 });
      }
      expect(init?.credentials).toBe('same-origin');
      return signedIn
        ? json({ ...testProfile, unexpectedSecret: 'do-not-cache' })
        : json({ code: 'unauthorized' }, 401);
    });
    const client = mount('/admin/login', { returnTo: '//evil.example' });
    await fillLogin();
    await screen.findByRole('heading', { name: 'Dashboard' });
    expect(screen.getByLabelText('Current path')).toHaveTextContent(
      /^\/admin$/,
    );
    expect(client.getQueryData(queryKeys.auth.me())).toEqual(testProfile);
    expect(client.getMutationCache().getAll()).toEqual([]);
    expect(
      JSON.stringify(
        client
          .getQueryCache()
          .getAll()
          .map((query) => query.state.data),
      ),
    ).not.toContain('password');
    const loginRequest = fetch.mock.calls.find(([url]) =>
      requestPath(url).endsWith('/login'),
    );
    expect(loginRequest?.[1]?.headers).toMatchObject({
      'X-CSRF-TOKEN': 'anonymous-csrf',
    });
    act(() => {
      client.setQueryData(queryKeys.users.list(), {
        private: 'previous-account',
      });
    });
    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));
    await screen.findByRole('button', { name: 'Sign in' });
    expect(client.getQueryData(queryKeys.users.list())).toBeUndefined();
    expect(client.getQueryData(queryKeys.auth.me())).toBeNull();
    const logoutRequest = fetch.mock.calls.find(([url]) =>
      requestPath(url).endsWith('/logout'),
    );
    expect(logoutRequest?.[1]?.headers).toMatchObject({
      'X-CSRF-TOKEN': 'authenticated-csrf',
    });
    expect(localStorage.length + sessionStorage.length).toBe(0);
  });

  it.each([401, 400, 429, 503])(
    'renders safe login error %s and never retries credentials',
    async (status) => {
      const fetch = mockFetch((url) => {
        if (requestPath(url).endsWith('/csrf')) return json({ token: 'csrf' });
        if (requestPath(url).endsWith('/login'))
          return json(
            {
              code: status === 400 ? 'csrf_invalid' : 'denied',
              detail: 'secret detail',
            },
            status,
          );
        return json({}, 401);
      });
      mount();
      await fillLogin();
      expect(await screen.findByRole('alert')).toHaveTextContent(
        'Unable to sign in',
      );
      await waitFor(() =>
        expect(screen.getByLabelText('Password')).toHaveValue(''),
      );
      expect(
        fetch.mock.calls.filter(([url]) => requestPath(url).endsWith('/login')),
      ).toHaveLength(1);
      expect(screen.queryByText('secret detail')).not.toBeInTheDocument();
      expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    },
  );

  it('validates empty and malformed input before submitting', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(json({}, 401));
    mount();
    fireEvent.click(await screen.findByRole('button', { name: 'Sign in' }));
    await screen.findByText('Enter your email address.');
    expect(fetch).toHaveBeenCalledTimes(1);
    fireEvent.change(screen.getByLabelText('Email'), {
      target: { value: 'invalid' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await screen.findByText('Enter a valid email address.');
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it('prevents double submit while a login request is in flight', async () => {
    let complete: ((value: Response) => void) | undefined;
    const fetch = mockFetch((url) => {
      if (requestPath(url).endsWith('/csrf')) return json({ token: 'csrf' });
      if (requestPath(url).endsWith('/login'))
        return new Promise<Response>((resolve) => {
          complete = resolve;
        });
      return json({}, 401);
    });
    mount('/admin/login');
    await fillLogin();
    await waitFor(() => expect(complete).toBeDefined());
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeDisabled();
    fireEvent.submit(screen.getByLabelText('Password').closest('form')!);
    expect(
      fetch.mock.calls.filter(([url]) => requestPath(url).endsWith('/login')),
    ).toHaveLength(1);
    act(() => {
      complete?.(json({}, 401));
    });
  });

  it('403 renders Forbidden without a login redirect loop', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(json({ code: 'forbidden' }, 403));
    mount('/admin/users');
    await screen.findByRole('heading', { name: 'Access denied' });
    expect(screen.getByLabelText('Current path')).toHaveTextContent(
      '/admin/users',
    );
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it('unknown roles fail closed and ReadOnly receives read capability only', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(json({ ...testProfile, role: 'Unknown' }));
    mount();
    await screen.findByRole('heading', { name: 'Access denied' });
    fetch.mockResolvedValue(json({ ...testProfile, role: 'IdentityReadOnly' }));
    fireEvent.click(screen.getByRole('button', { name: 'Check access again' }));
    await screen.findByText('Read only');
    expect(
      screen.queryByText('Administrator', { exact: true }),
    ).not.toBeInTheDocument();
  });

  it('an expired session on any admin request hides content and clears cached data', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation(() => Promise.resolve(json(testProfile)));
    const client = mount('/admin/missing');
    await screen.findByRole('heading', { name: 'Page not found' });
    act(() => {
      client.setQueryData(queryKeys.dashboard(), { private: 'old' });
    });
    fetch.mockImplementation(() => Promise.resolve(json({}, 401)));
    await act(async () => {
      await apiClient
        .request('get', '/api/v1/admin/dashboard', {})
        .catch(() => undefined);
    });
    await screen.findByRole('button', { name: 'Sign in' });
    expect(client.getQueryData(queryKeys.dashboard())).toBeUndefined();
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Current path')).toHaveTextContent(
      '/admin/login',
    );
  });

  it('logout network failure keeps the session and permits a manual retry', async () => {
    mockFetch((url) => {
      if (requestPath(url).endsWith('/csrf')) return json({ token: 'csrf' });
      if (requestPath(url).endsWith('/logout')) throw new Error('raw secret');
      return json(testProfile);
    });
    mount();
    fireEvent.click(await screen.findByRole('button', { name: 'Sign out' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Sign out failed',
    );
    expect(screen.getByRole('heading', { name: 'Dashboard' })).toBeVisible();
    expect(screen.queryByText('raw secret')).not.toBeInTheDocument();
  });

  it('a session-check network error has an explicit GET retry without exposing the shell', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockRejectedValue(new Error('private diagnostic'));
    mount();
    await screen.findByRole('alert');
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    fetch.mockImplementation(() => Promise.resolve(json(testProfile)));
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    await screen.findByRole('heading', { name: 'Dashboard' });
    expect(screen.queryByText('private diagnostic')).not.toBeInTheDocument();
  });

  it.each([
    'https://evil.test',
    '//evil.test',
    '/admin\\evil',
    '/admin/login',
    '/admin/login/next',
    '/admin?token=secret',
    '/admin#secret',
    '/admin/%2f%2fevil',
  ])('rejects unsafe return path %s', (path) => {
    expect(safeReturnPath(path)).toBe('/admin');
  });
  it('preserves an internal admin destination', () => {
    expect(safeReturnPath('/admin/users/abc-123')).toBe('/admin/users/abc-123');
  });
});
