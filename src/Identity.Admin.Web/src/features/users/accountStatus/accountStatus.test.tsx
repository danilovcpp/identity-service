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
import { AccountStatusAction } from './AccountStatusAction';
import { useAccountStatusCommand } from './useAccountStatusCommand';

const id = account.id;
const json = (value: unknown, status = 200) =>
  new Response(JSON.stringify(value), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
function responses(
  response: () => Response | Promise<Response> = () =>
    new Response(null, { status: 204 }),
) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
    const path =
      typeof input === 'string'
        ? input
        : input instanceof URL
          ? input.href
          : input.url;
    return path.endsWith('/csrf')
      ? Promise.resolve(json({ token: 'test-status-csrf' }))
      : Promise.resolve().then(response);
  });
}
function mount(status = 'Active', canWrite = true) {
  const client = createQueryClient();
  const keys = [
    queryKeys.users.list({ Page: 1 }),
    queryKeys.users.detail(id),
    queryKeys.users.sessions(id, { page: 2, state: 'active' }),
    queryKeys.dashboard(),
    queryKeys.audit.list({ UserId: id }),
  ];
  for (const key of keys) client.setQueryData(key, { testData: true });
  client.setQueryData(queryKeys.auth.me(), { testProfile: true });
  const view = render(
    <QueryClientProvider client={client}>
      <ConfigProvider theme={{ token: { motion: false } }}>
        <App>
          <TestAuth canWrite={canWrite}>
            <AccountStatusAction
              userId={id}
              email={account.email}
              status={status}
            />
          </TestAuth>
        </App>
      </ConfigProvider>
    </QueryClientProvider>,
  );
  return { ...view, client, keys };
}
async function open(disabling = true) {
  fireEvent.click(
    screen.getByRole('button', {
      name: disabling ? 'Disable account' : 'Enable account',
    }),
  );
  const dialog = await screen.findByRole('dialog');
  await waitFor(() => expect(dialog).toBeVisible());
  return dialog;
}
function reason(dialog: HTMLElement, value = '  Manual security review  ') {
  fireEvent.change(within(dialog).getByLabelText('Reason'), {
    target: { value },
  });
}
function confirm(dialog: HTMLElement, disabling = true) {
  fireEvent.click(
    within(dialog).getByRole('button', {
      name: disabling ? 'Confirm disable' : 'Confirm enable',
    }),
  );
}
beforeEach(() => {
  apiClient.clearCsrfToken();
  // jsdom has no layout. Browser tests verify textarea sizing at three widths.
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

describe('account status commands', () => {
  it('cancels submission if the dialog closes while form validation is still running', async () => {
    const fetch = responses();
    mount();
    const dialog = await open();
    reason(dialog);
    confirm(dialog);
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    );
    expect(fetch).not.toHaveBeenCalled();
  });
  it.each([
    'Active',
    'PendingVerification',
    'Invited',
    'Disabled',
    'Deactivated',
  ])('hides all status controls from ReadOnly for %s', (status) => {
    const fetch = responses();
    mount(status, false);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });
  it.each(['Active', 'PendingVerification', 'Invited'])(
    'only offers Disable for %s',
    (status) => {
      mount(status);
      expect(
        screen.getByRole('button', { name: 'Disable account' }),
      ).toBeVisible();
      expect(
        screen.queryByRole('button', { name: 'Enable account' }),
      ).not.toBeInTheDocument();
    },
  );
  it.each(['Deactivated', 'Unknown', '<script>invalid</script>'])(
    'does not offer a transition for %s',
    (status) => {
      const fetch = responses();
      mount(status);
      expect(screen.queryByRole('button')).not.toBeInTheDocument();
      expect(fetch).not.toHaveBeenCalled();
    },
  );
  it.each([
    '',
    '   ',
    'a'.repeat(501),
    'Security\nreview',
    'Security\u0085review',
  ])('rejects an invalid reason (%j) before CSRF or POST', async (value) => {
    const fetch = responses();
    mount();
    const dialog = await open();
    reason(dialog, value);
    confirm(dialog);
    await waitFor(() =>
      expect(dialog.querySelector('.ant-form-item-has-error')).not.toBeNull(),
    );
    expect(fetch).not.toHaveBeenCalled();
  });
  it('explains session/token effects, cancels without a request and clears the reason', async () => {
    const fetch = responses();
    mount();
    let dialog = await open();
    expect(
      within(dialog).getByText(
        /revokes all refresh and administrator sessions/,
      ),
    ).toBeVisible();
    expect(within(dialog).getByText(account.email)).toBeVisible();
    reason(dialog);
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    );
    expect(fetch).not.toHaveBeenCalled();
    dialog = await open();
    expect(within(dialog).getByLabelText('Reason')).toHaveValue('');
  });
  it('waits for 204, sends only trimmed reason with CSRF, prevents doubles and invalidates reads including inactive sessions', async () => {
    let complete: ((response: Response) => void) | undefined;
    const fetch = responses(
      () =>
        new Promise<Response>((resolve) => {
          complete = resolve;
        }),
    );
    const { client, keys } = mount();
    const dialog = await open();
    reason(dialog);
    confirm(dialog);
    confirm(dialog);
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2));
    expect(fetch).toHaveBeenLastCalledWith(
      `/api/v1/admin/users/${id}/disable`,
      expect.objectContaining({
        method: 'POST',
        credentials: 'same-origin',
        body: JSON.stringify({ reason: 'Manual security review' }),
        headers: expect.objectContaining({
          'X-CSRF-TOKEN': 'test-status-csrf',
        }) as HeadersInit,
      }),
    );
    expect(
      within(dialog).getByRole('button', { name: 'Confirm disable' }),
    ).toBeDisabled();
    expect(
      within(dialog).getByRole('button', { name: 'Cancel' }),
    ).toBeDisabled();
    expect(within(dialog).getByLabelText('Reason')).toBeDisabled();
    expect(screen.queryByText('Account disabled.')).not.toBeInTheDocument();
    expect(
      keys.every((key) => client.getQueryState(key)?.isInvalidated === false),
    ).toBe(true);
    await act(async () => {
      complete?.(new Response(null, { status: 204 }));
      await Promise.resolve();
    });
    expect(await screen.findByText('Account disabled.')).toBeVisible();
    expect(
      keys.every((key) => client.getQueryState(key)?.isInvalidated === true),
    ).toBe(true);
    expect(client.getQueryState(queryKeys.auth.me())?.isInvalidated).toBe(
      false,
    );
  });
  it('confirms Enable separately with no reason/body and leaves the restored state to the server', async () => {
    const fetch = responses();
    const { client, keys } = mount('Disabled');
    expect(
      screen.queryByRole('button', { name: 'Disable account' }),
    ).not.toBeInTheDocument();
    const dialog = await open(false);
    expect(fetch).not.toHaveBeenCalled();
    expect(within(dialog).queryByLabelText('Reason')).not.toBeInTheDocument();
    expect(
      within(dialog).getByText(/Identity determines the restored status/),
    ).toBeVisible();
    confirm(dialog, false);
    await screen.findByText(
      'Account enabled. Identity will refresh its status.',
    );
    expect(fetch).toHaveBeenLastCalledWith(
      `/api/v1/admin/users/${id}/enable`,
      expect.objectContaining({ method: 'POST', body: undefined }),
    );
    for (const key of keys)
      expect(client.getQueryData(key)).toEqual({ testData: true });
  });
  it.each([
    [
      409,
      'last_administrator',
      'The last active administrator cannot be disabled',
    ],
    [409, 'admin_user_conflict', 'The account state has changed'],
    [403, 'forbidden', 'You do not have permission'],
    [400, 'csrf_invalid', 'Your security check expired'],
    [404, 'user_not_found', 'The requested resource was not found'],
    [429, 'rate_limited', 'Too many requests'],
  ])(
    'handles %i / %s without success, replay or invalidation',
    async (status, code, text) => {
      const fetch = responses(() =>
        json({ code, detail: 'server-secret' }, status),
      );
      const { client, keys } = mount();
      const dialog = await open();
      reason(dialog);
      confirm(dialog);
      await waitFor(() =>
        expect(screen.getByText(new RegExp(text))).toBeVisible(),
      );
      expect(screen.getByRole('dialog')).toBeVisible();
      expect(within(dialog).getByLabelText('Reason')).toHaveValue(
        '  Manual security review  ',
      );
      expect(screen.queryByText('Account disabled.')).not.toBeInTheDocument();
      expect(screen.queryByText('server-secret')).not.toBeInTheDocument();
      expect(fetch).toHaveBeenCalledTimes(2);
      expect(
        keys.every((key) => client.getQueryState(key)?.isInvalidated === false),
      ).toBe(true);
    },
  );
  it('maps backend Reason validation to fixed safe field feedback', async () => {
    responses(() =>
      json(
        {
          code: 'validation_failed',
          errors: { Reason: ['raw reason secret'] },
        },
        400,
      ),
    );
    mount();
    const dialog = await open();
    reason(dialog);
    confirm(dialog);
    expect(
      await screen.findByText(
        'Enter a reason of at most 500 characters without control characters.',
      ),
    ).toBeVisible();
    expect(screen.queryByText('raw reason secret')).not.toBeInTheDocument();
  });
  it.each(['Active', 'Disabled'])(
    'does not replay %s commands on network failure',
    async (status) => {
      const fetch = responses(() =>
        Promise.reject(new TypeError('raw network secret')),
      );
      mount(status);
      const disabling = status !== 'Disabled';
      const dialog = await open(disabling);
      if (disabling) reason(dialog);
      confirm(dialog, disabling);
      expect(
        await screen.findByText(/The result could not be confirmed/),
      ).toBeVisible();
      expect(screen.queryByText('raw network secret')).not.toBeInTheDocument();
      expect(fetch).toHaveBeenCalledTimes(2);
    },
  );
  it('cancels an immediately unmounted command before HTTP', async () => {
    const fetch = responses();
    const client = createQueryClient();
    const { result, unmount } = renderHook(useAccountStatusCommand, {
      wrapper: ({ children }) => (
        <QueryClientProvider client={client}>{children}</QueryClientProvider>
      ),
    });
    const operation = result.current
      .execute({ action: 'enable', userId: id })
      .catch((error: unknown) => error);
    unmount();
    await expect(operation).resolves.toBeInstanceOf(ClientError);
    expect(fetch).not.toHaveBeenCalled();
  });
  it('aborts an in-flight command when its feature unmounts', async () => {
    const fetch = responses(() => new Promise<Response>(() => undefined));
    const view = mount();
    const dialog = await open();
    reason(dialog);
    confirm(dialog);
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2));
    const options = fetch.mock.calls.at(-1)?.[1];
    view.unmount();
    expect(options?.signal?.aborted).toBe(true);
  });
});
