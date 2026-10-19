import { QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { ConfigProvider } from 'antd';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '@/app/queryClient';
import { queryKeys } from '@/api/queryKeys';
import { ClientError } from '@/api/errors';
import { TestAuth } from '@/test/TestAuth';
import { safeReturnPath } from '@/features/auth/returnPath';
import {
  testUserDetails as account,
  testUserEvent as event,
} from '@/test/userDetails';
import { UserDetailsPage } from './UserDetailsPage';
import { getUserDetails, getUserActivity } from './detailsApi';

const id = account.id;
const json = (data: unknown, status = 200) =>
  new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
const path = (input: RequestInfo | URL) =>
  typeof input === 'string'
    ? input
    : input instanceof URL
      ? input.href
      : input.url;
function responses(detail: unknown = account, events: unknown[] = [event]) {
  return vi
    .spyOn(globalThis, 'fetch')
    .mockImplementation((input) =>
      Promise.resolve(
        path(input).includes('/audit-events')
          ? json({ items: events, page: 1, pageSize: 10, total: events.length })
          : path(input).includes('/sessions')
            ? json({ items: [], page: 1, pageSize: 25, total: 0 })
            : json(detail),
      ),
    );
}
function Location() {
  const location = useLocation();
  return (
    <output aria-label="Location">{location.pathname + location.search}</output>
  );
}
function mount(route = `/admin/users/${id}`) {
  const client = createQueryClient();
  const view = render(
    <QueryClientProvider client={client}>
      <ConfigProvider theme={{ token: { motion: false } }}>
        <TestAuth>
          <MemoryRouter initialEntries={[route]}>
            <Location />
            <Routes>
              <Route
                path="/admin/users/:userId"
                element={<UserDetailsPage />}
              />
              <Route path="/admin/users" element={<h1>Users destination</h1>} />
            </Routes>
          </MemoryRouter>
        </TestAuth>
      </ConfigProvider>
    </QueryClientProvider>,
  );
  return { ...view, client };
}
const profile = () =>
  screen.findByRole('region', { name: 'Account information' });
const clipboardDescriptor = Object.getOwnPropertyDescriptor(
  navigator,
  'clipboard',
);
afterEach(() => {
  if (clipboardDescriptor)
    Object.defineProperty(navigator, 'clipboard', clipboardDescriptor);
  else Reflect.deleteProperty(navigator, 'clipboard');
});

describe('read-only user details', () => {
  it.each([
    'not-a-uuid',
    '00000000-0000-0000-0000-000000000000',
    '12345678-c939-44af-90fe-zzzzzzzzzzzz',
  ])('rejects invalid UUID %s before either GET', async (value) => {
    const fetch = responses();
    mount('/admin/users/' + value);
    expect(
      await screen.findByRole('heading', { name: 'Invalid user ID' }),
    ).toBeVisible();
    expect(fetch).not.toHaveBeenCalled();
    expect(screen.getByRole('link', { name: 'Back to users' })).toHaveAttribute(
      'href',
      '/admin/users',
    );
  });
  it('shows loading and waits for the account before requesting activity', () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockReturnValue(new Promise(() => undefined));
    mount();
    expect(screen.getByRole('status', { name: 'Loading user…' })).toBeVisible();
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(
      screen.queryByRole('region', { name: 'Recent security activity' }),
    ).not.toBeInTheDocument();
  });
  it('renders nullable profile fields, local timestamps/UTC titles and bounded server-filtered activity', async () => {
    const fetch = responses();
    mount();
    const card = within(await profile());
    expect(card.getByText(account.email)).toBeVisible();
    expect(card.getByText('Not provided')).toBeVisible();
    expect(card.getByText('Not disabled')).toBeVisible();
    expect(card.getByText('Not deactivated')).toBeVisible();
    expect(card.getByText('Verified', { exact: true })).toBeVisible();
    const created = document.querySelector(
      `time[datetime="${account.createdAt}"]`,
    );
    expect(created).toHaveAttribute('title', '2026-10-19T12:00:00.000Z');
    expect(await screen.findByText('verify_email')).toBeVisible();
    expect(fetch).toHaveBeenCalledWith(
      `/api/v1/admin/users/${id}`,
      expect.objectContaining({
        credentials: 'same-origin',
        signal: expect.any(AbortSignal) as AbortSignal,
      }),
    );
    expect(fetch).toHaveBeenCalledWith(
      `/api/v1/admin/audit-events?Page=1&PageSize=10&UserId=${id}`,
      expect.objectContaining({ method: 'GET' }),
    );
  });
  it.each([
    'Active',
    'PendingVerification',
    'Invited',
    'Disabled',
    'Deactivated',
  ])('displays %s without activating any account actions', async (status) => {
    responses({
      ...account,
      status,
      emailVerified: false,
      emailVerifiedAt: null,
      disabledAt: status === 'Disabled' ? account.updatedAt : null,
      deactivatedAt: status === 'Deactivated' ? account.updatedAt : null,
    });
    mount();
    const region = await profile();
    expect(
      within(region).getByText(
        status === 'PendingVerification' ? 'Pending verification' : status,
      ),
    ).toBeVisible();
    expect(within(region).getAllByText('Not verified')).toHaveLength(2);
    expect(
      screen.queryByRole('button', {
        name: /invite|resend|disable|enable|password|revoke|edit/i,
      }),
    ).not.toBeInTheDocument();
  });
  it('renders 404 separately without requesting audit or presenting account actions', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation(() =>
        Promise.resolve(
          json({ code: 'user_not_found', detail: 'private' }, 404),
        ),
      );
    mount();
    expect(
      await screen.findByRole('heading', { name: 'User not found' }),
    ).toBeVisible();
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(screen.queryByText('private')).not.toBeInTheDocument();
  });
  it('handles account failure with a manual GET retry', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation(() => Promise.resolve(json({}, 500)));
    mount();
    await screen.findByRole('alert');
    expect(fetch).toHaveBeenCalledTimes(1);
    fetch.mockImplementation((input) =>
      Promise.resolve(
        path(input).includes('/audit-events')
          ? json({ items: [], page: 1, pageSize: 10, total: 0 })
          : path(input).includes('/sessions')
            ? json({ items: [], page: 1, pageSize: 25, total: 0 })
            : json(account),
      ),
    );
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    await profile();
    expect(
      await screen.findByRole('heading', {
        name: 'No recent security activity',
      }),
    ).toBeVisible();
  });
  it('keeps account data visible when activity fails and retries only that GET', async () => {
    let fail = true;
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation((input) =>
        Promise.resolve(
          path(input).includes('/audit-events')
            ? fail
              ? json({}, 500)
              : json({ items: [event], page: 1, pageSize: 10, total: 1 })
            : path(input).includes('/sessions')
              ? json({ items: [], page: 1, pageSize: 25, total: 0 })
              : json(account),
        ),
      );
    mount();
    await screen.findByRole('alert');
    expect(within(await profile()).getByText(account.email)).toBeVisible();
    fail = false;
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    await screen.findByText(event.eventType);
    expect(
      fetch.mock.calls.filter(
        ([input]) => path(input) === `/api/v1/admin/users/${id}`,
      ),
    ).toHaveLength(1);
  });
  it('preserves list filters in the detail URL and returns safely after a direct load', async () => {
    responses();
    const filters =
      'page=2&pageSize=50&search=alice&status=Active&emailVerified=true&sortBy=email&sortDirection=asc';
    mount(`/admin/users/${id}?${filters}&token=discarded`);
    await profile();
    expect(screen.getByLabelText('Location')).toHaveTextContent(
      `/admin/users/${id}?${filters}`,
    );
    expect(screen.getByLabelText('Location')).not.toHaveTextContent(
      'discarded',
    );
    expect(
      safeReturnPath(
        `/admin/users/${id}?${filters}&returnTo=https://evil.example`,
      ),
    ).toBe(`/admin/users/${id}?${filters}`);
    fireEvent.click(screen.getByRole('link', { name: 'Back to users' }));
    expect(screen.getByLabelText('Location')).toHaveTextContent(
      '/admin/users?' + filters,
    );
    expect(
      screen.getByRole('heading', { name: 'Users destination' }),
    ).toBeVisible();
  });
  it('escapes profile/event text and never puts sensitive fields or metadata in DOM/cache', async () => {
    const marker = 'sensitive-value-must-not-appear';
    responses(
      {
        ...account,
        displayName: '<img src=x onerror=alert(1)>',
        passwordHash: marker,
        refreshToken: marker,
        disableReason: marker,
        normalizedEmail: marker,
      },
      [
        {
          ...event,
          eventType: '<script>bad</script>',
          metadata: marker,
          password: marker,
          correlationId: marker,
        },
      ],
    );
    const { client } = mount();
    expect(await screen.findByText('<script>bad</script>')).toBeVisible();
    expect(screen.getByText('<img src=x onerror=alert(1)>')).toBeVisible();
    expect(document.querySelector('script')).toBeNull();
    expect(screen.queryByText(marker)).not.toBeInTheDocument();
    expect(
      JSON.stringify(client.getQueryData(queryKeys.users.detail(id))),
    ).not.toContain(marker);
    expect(
      JSON.stringify(client.getQueryData(queryKeys.audit.userActivity(id))),
    ).not.toContain(marker);
  });
  it('copies only the user ID and handles clipboard denial without a raw error', async () => {
    const clipboard = {
      writeText: vi
        .fn<(value: string) => Promise<void>>()
        .mockResolvedValue(undefined),
    };
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: clipboard,
    });
    responses();
    mount();
    await profile();
    fireEvent.click(screen.getByRole('button', { name: 'Copy user ID' }));
    expect(await screen.findByText('User ID copied.')).toBeVisible();
    expect(clipboard.writeText).toHaveBeenCalledWith(id);
    clipboard.writeText.mockRejectedValue(new Error('private error'));
    fireEvent.click(screen.getByRole('button', { name: 'Copy user ID' }));
    expect(
      await screen.findByText('Copy unavailable. Select the ID to copy it.'),
    ).toBeVisible();
    expect(screen.queryByText('private error')).not.toBeInTheDocument();
  });
  it('aborts a pending account request on unmount', () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockReturnValue(new Promise(() => undefined));
    const { unmount } = mount();
    const signal = fetch.mock.calls[0]?.[1]?.signal;
    unmount();
    expect(signal?.aborted).toBe(true);
  });
  it('aborts pending activity on unmount', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation((input) =>
        path(input).includes('/audit-events')
          ? new Promise(() => undefined)
          : Promise.resolve(json(account)),
      );
    const { unmount } = mount();
    await screen.findByRole('status', { name: 'Loading security activity…' });
    const signal = fetch.mock.calls.find(([input]) =>
      path(input).includes('/audit-events'),
    )?.[1]?.signal;
    unmount();
    expect(signal?.aborted).toBe(true);
  });
  it.each([
    { ...account, createdAt: 'invalid' },
    { ...account, id: event.id },
    { ...account, disabledAt: false },
    { ...account, displayName: undefined },
  ])('rejects malformed account responses', async (data) => {
    responses(data);
    await expect(
      getUserDetails(id, new AbortController().signal),
    ).rejects.toBeInstanceOf(ClientError);
  });
  it('rejects events unrelated to the account instead of displaying another user activity', async () => {
    responses(account, [{ ...event, targetUserId: event.id }]);
    await expect(
      getUserActivity(id, new AbortController().signal),
    ).rejects.toBeInstanceOf(ClientError);
  });
  it('hides stale account data after a failed refresh', async () => {
    const fetch = responses();
    mount();
    await screen.findByText(event.eventType);
    fetch.mockImplementation(() => Promise.resolve(json({}, 500)));
    fireEvent.click(
      screen.getByRole('button', { name: 'Refresh user details' }),
    );
    await screen.findByRole('alert');
    expect(
      screen.queryByRole('region', { name: 'Account information' }),
    ).not.toBeInTheDocument();
  });
});
