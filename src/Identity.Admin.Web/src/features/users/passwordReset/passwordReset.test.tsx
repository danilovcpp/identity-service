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
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '@/app/queryClient';
import { apiClient } from '@/api/httpClient';
import { ClientError } from '@/api/errors';
import { queryKeys } from '@/api/queryKeys';
import { TestAuth } from '@/test/TestAuth';
import { testUserDetails as account } from '@/test/userDetails';
import { RequestPasswordReset } from './RequestPasswordReset';
import { usePasswordResetCommand } from './usePasswordResetCommand';

const id = account.id;
const success = 'Password reset request accepted for processing.';
const json = (value: unknown, status = 200) =>
  new Response(JSON.stringify(value), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
function responses(
  response: () => Response | Promise<Response> = () =>
    new Response(null, { status: 202 }),
) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
    const url =
      typeof input === 'string'
        ? input
        : input instanceof URL
          ? input.href
          : input.url;
    return url.endsWith('/csrf')
      ? Promise.resolve(json({ token: 'test-reset-csrf' }))
      : Promise.resolve().then(response);
  });
}
function mount({
  status = 'Active',
  emailVerified = true,
  canWrite = true,
} = {}) {
  const client = createQueryClient();
  const auditKeys = [
    queryKeys.audit.list({ UserId: id }),
    queryKeys.audit.list({ Page: 2 }),
  ];
  const unchangedKeys = [
    queryKeys.users.detail(id),
    queryKeys.users.list(),
    queryKeys.users.sessions(id),
    queryKeys.dashboard(),
    queryKeys.auth.me(),
  ];
  for (const key of [...auditKeys, ...unchangedKeys])
    client.setQueryData(key, { testData: true });
  const view = render(
    <QueryClientProvider client={client}>
      <ConfigProvider theme={{ token: { motion: false } }}>
        <App>
          <TestAuth canWrite={canWrite}>
            <RequestPasswordReset
              userId={id}
              email={account.email}
              status={status}
              emailVerified={emailVerified}
            />
          </TestAuth>
        </App>
      </ConfigProvider>
    </QueryClientProvider>,
  );
  return { ...view, client, auditKeys, unchangedKeys };
}
async function open() {
  fireEvent.click(
    screen.getByRole('button', {
      name: 'Send password reset link',
    }),
  );
  const dialog = await screen.findByRole('dialog', {
    name: 'Send password reset link?',
  });
  await waitFor(() => expect(dialog).toBeVisible());
  return dialog;
}
function confirm(dialog: HTMLElement) {
  fireEvent.click(
    within(dialog).getByRole('button', { name: 'Request reset email' }),
  );
}
beforeEach(() => apiClient.clearCsrfToken());

describe('administrator password reset request', () => {
  it.each([
    'Active',
    'PendingVerification',
    'Invited',
    'Disabled',
    'Deactivated',
  ])('hides the action from ReadOnly for %s', (status) => {
    const fetch = responses();
    mount({ status, canWrite: false });
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });
  it.each([
    'PendingVerification',
    'Invited',
    'Disabled',
    'Deactivated',
    'Unknown',
  ])('does not offer reset for %s', (status) => {
    const fetch = responses();
    mount({ status });
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });
  it('does not offer reset when the email is unverified', () => {
    mount({ emailVerified: false });
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
  it('only opens a confirmation, explains the recipient and effects, and cancels without HTTP', async () => {
    const fetch = responses();
    mount();
    const dialog = await open();
    expect(within(dialog).getByText(account.email)).toBeVisible();
    expect(dialog).toHaveTextContent('administrators cannot see or set it');
    expect(dialog).toHaveTextContent(
      'Sessions are revoked when the recipient completes the reset',
    );
    expect(dialog).toHaveTextContent('Email delivery is not guaranteed');
    expect(within(dialog).queryByRole('textbox')).not.toBeInTheDocument();
    expect(dialog.querySelector('input[type=password]')).toBeNull();
    expect(fetch).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    );
    expect(fetch).not.toHaveBeenCalled();
  });
  it('waits for 202, posts no body with CSRF, guards double confirmation and invalidates only audit', async () => {
    let complete: ((response: Response) => void) | undefined;
    const fetch = responses(
      () =>
        new Promise<Response>((resolve) => {
          complete = resolve;
        }),
    );
    const { client, auditKeys, unchangedKeys } = mount();
    const dialog = await open();
    confirm(dialog);
    confirm(dialog);
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2));
    expect(fetch).toHaveBeenLastCalledWith(
      `/api/v1/admin/users/${id}/password-reset`,
      expect.objectContaining({
        method: 'POST',
        credentials: 'same-origin',
        body: undefined,
        signal: expect.any(AbortSignal) as AbortSignal,
        headers: expect.objectContaining({
          'X-CSRF-TOKEN': 'test-reset-csrf',
        }) as HeadersInit,
      }),
    );
    expect(
      within(dialog).getByRole('button', { name: 'Request reset email' }),
    ).toBeDisabled();
    expect(
      within(dialog).getByRole('button', { name: 'Cancel' }),
    ).toBeDisabled();
    expect(screen.queryByText(success)).not.toBeInTheDocument();
    expect(
      auditKeys.every(
        (key) => client.getQueryState(key)?.isInvalidated === false,
      ),
    ).toBe(true);
    await act(async () => {
      complete?.(new Response(null, { status: 202 }));
      await Promise.resolve();
    });
    expect(await screen.findByText(success)).toBeVisible();
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    );
    expect(
      auditKeys.every(
        (key) => client.getQueryState(key)?.isInvalidated === true,
      ),
    ).toBe(true);
    expect(
      unchangedKeys.every(
        (key) => client.getQueryState(key)?.isInvalidated === false,
      ),
    ).toBe(true);
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });
  it.each([
    [403, 'forbidden', 'You do not have permission'],
    [409, 'admin_user_conflict', 'This account is no longer eligible'],
    [400, 'csrf_invalid', 'Your security check expired'],
    [404, 'user_not_found', 'The requested resource was not found'],
    [429, 'rate_limited', 'Too many requests'],
    [503, 'password_reset_unavailable', 'Password reset is unavailable'],
    [500, 'internal_error', 'Identity could not complete the request'],
  ])(
    'handles %i / %s without success, retry or invalidation',
    async (status, code, text) => {
      const fetch = responses(() =>
        json(
          {
            code,
            detail: 'raw-secret',
            token: 'raw-token',
            password: 'raw-password',
          },
          status,
        ),
      );
      const { client, auditKeys } = mount();
      const dialog = await open();
      confirm(dialog);
      expect(await screen.findByText(new RegExp(text))).toBeVisible();
      expect(screen.getByRole('dialog')).toBeVisible();
      expect(screen.queryByText(success)).not.toBeInTheDocument();
      expect(document.body).not.toHaveTextContent(
        /raw-secret|raw-token|raw-password/,
      );
      expect(fetch).toHaveBeenCalledTimes(2);
      expect(
        auditKeys.every(
          (key) => client.getQueryState(key)?.isInvalidated === false,
        ),
      ).toBe(true);
      expect(
        within(dialog).getByRole('button', { name: 'Request reset email' }),
      ).toBeEnabled();
    },
  );
  it('does not replay a network failure or claim that email was sent; reopening clears the error', async () => {
    const fetch = responses(() => Promise.reject(new TypeError('raw-secret')));
    mount();
    const dialog = await open();
    confirm(dialog);
    expect(
      await screen.findByText(/The result could not be confirmed/),
    ).toBeVisible();
    expect(screen.queryByText(success)).not.toBeInTheDocument();
    expect(document.body).not.toHaveTextContent('raw-secret');
    expect(fetch).toHaveBeenCalledTimes(2);
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    );
    const reopened = await open();
    expect(within(reopened).queryByRole('alert')).not.toBeInTheDocument();
    expect(fetch).toHaveBeenCalledTimes(2);
  });
  it('cancels an immediately unmounted command before HTTP', async () => {
    const fetch = responses();
    const client = createQueryClient();
    const { result, unmount } = renderHook(usePasswordResetCommand, {
      wrapper: ({ children }) => (
        <QueryClientProvider client={client}>{children}</QueryClientProvider>
      ),
    });
    const operation = result.current
      .execute(id)
      .catch((error: unknown) => error);
    unmount();
    await expect(operation).resolves.toBeInstanceOf(ClientError);
    expect(fetch).not.toHaveBeenCalled();
  });
  it('aborts an in-flight request on unmount', async () => {
    const fetch = responses(() => new Promise<Response>(() => undefined));
    const view = mount();
    confirm(await open());
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2));
    const options = fetch.mock.calls.at(-1)?.[1];
    view.unmount();
    expect(options?.signal?.aborted).toBe(true);
  });
});
