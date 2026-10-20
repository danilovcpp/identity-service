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
import { queryKeys } from '@/api/queryKeys';
import { ClientError } from '@/api/errors';
import { TestAuth } from '@/test/TestAuth';
import { testUserDetails } from '@/test/userDetails';
import { InviteUser } from './InviteUser';
import { ResendInvitation } from './ResendInvitation';
import { useInvitationCommand } from './useInvitationCommand';

const id = testUserDetails.id;
const json = (data: unknown, status = 200) =>
  new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
function mockCommand(
  response: () => Response | Promise<Response> = () =>
    new Response(null, { status: 202 }),
) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
    const path =
      typeof input === 'string'
        ? input
        : input instanceof URL
          ? input.href
          : input.url;
    if (path.endsWith('/csrf'))
      return Promise.resolve(json({ token: 'test-csrf-value' }));
    return Promise.resolve().then(response);
  });
}
function mount(canWrite = true, status = 'Invited') {
  const client = createQueryClient();
  const invalidate = vi.spyOn(client, 'invalidateQueries');
  const view = render(
    <QueryClientProvider client={client}>
      <ConfigProvider theme={{ token: { motion: false } }}>
        <App>
          <TestAuth canWrite={canWrite}>
            <InviteUser />
            <ResendInvitation userId={id} status={status} />
          </TestAuth>
        </App>
      </ConfigProvider>
    </QueryClientProvider>,
  );
  return { ...view, client, invalidate };
}
async function openInvite(email = 'invited@example.test', name?: string) {
  fireEvent.click(screen.getByRole('button', { name: 'Invite user' }));
  const dialog = await screen.findByRole('dialog', { name: 'Invite user' });
  await waitFor(() => expect(dialog).toBeVisible());
  fireEvent.change(within(dialog).getByLabelText('Email'), {
    target: { value: email },
  });
  if (name !== undefined)
    fireEvent.change(within(dialog).getByLabelText('Display name (optional)'), {
      target: { value: name },
    });
  return dialog;
}
function submit(dialog: HTMLElement) {
  const form = dialog.querySelector('form');
  if (!form) throw new Error('Invitation form is required.');
  fireEvent.submit(form);
}
beforeEach(() => apiClient.clearCsrfToken());

describe('invitation commands', () => {
  it('cancels before the mutation starts if navigation unmounts the feature immediately', async () => {
    const fetch = mockCommand();
    const client = createQueryClient();
    const { result, unmount } = renderHook(useInvitationCommand, {
      wrapper: ({ children }) => (
        <QueryClientProvider client={client}>{children}</QueryClientProvider>
      ),
    });
    const operation = result.current
      .execute({ kind: 'resend', userId: id })
      .catch((error: unknown) => error);
    unmount();
    await expect(operation).resolves.toBeInstanceOf(ClientError);
    expect(fetch).not.toHaveBeenCalled();
  });
  it('hides all write controls from ReadOnly, including an invited account', () => {
    const fetch = mockCommand();
    mount(false);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });
  it.each(['Active', 'PendingVerification', 'Disabled', 'Deactivated'])(
    'never offers resend for %s',
    (status) => {
      mount(true, status);
      expect(
        screen.queryByRole('button', { name: 'Resend invitation' }),
      ).not.toBeInTheDocument();
    },
  );
  it.each(['', 'invalid-email', 'someone\u0085@example.test'])(
    'rejects invalid email %j before CSRF or POST',
    async (email) => {
      const fetch = mockCommand();
      mount();
      const dialog = await openInvite(email);
      submit(dialog);
      await waitFor(() =>
        expect(dialog.querySelector('.ant-form-item-has-error')).not.toBeNull(),
      );
      expect(fetch).not.toHaveBeenCalled();
      expect(dialog.querySelector('input[type=password]')).toBeNull();
      expect(
        within(dialog).getByText(/recipient sets their own password/),
      ).toBeVisible();
    },
  );
  it('rejects a display name containing control characters', async () => {
    const fetch = mockCommand();
    mount();
    const dialog = await openInvite('invited@example.test', 'Bad\u0085name');
    submit(dialog);
    expect(
      await within(dialog).findByText('Remove control characters.'),
    ).toBeVisible();
    expect(fetch).not.toHaveBeenCalled();
  });
  it('confirms a trimmed invitation with CSRF, prevents double submission and invalidates users/dashboard/audit after 202', async () => {
    let complete: ((response: Response) => void) | undefined;
    const fetch = mockCommand(
      () =>
        new Promise<Response>((resolve) => {
          complete = resolve;
        }),
    );
    const { invalidate } = mount();
    const dialog = await openInvite('invited@example.test', '  New member  ');
    expect(fetch).not.toHaveBeenCalled();
    submit(dialog);
    submit(dialog);
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2));
    expect(fetch).toHaveBeenLastCalledWith(
      '/api/v1/admin/users/invitations',
      expect.objectContaining({
        method: 'POST',
        credentials: 'same-origin',
        headers: expect.objectContaining({
          'X-CSRF-TOKEN': 'test-csrf-value',
        }) as HeadersInit,
        body: JSON.stringify({
          email: 'invited@example.test',
          displayName: 'New member',
        }),
      }),
    );
    expect(
      within(dialog).getByRole('button', { name: 'Send invitation' }),
    ).toBeDisabled();
    expect(
      within(dialog).getByRole('button', { name: 'Cancel' }),
    ).toBeDisabled();
    expect(invalidate).not.toHaveBeenCalled();
    await act(async () => {
      complete?.(new Response(null, { status: 202 }));
      await Promise.resolve();
    });
    expect(
      await screen.findByText('Invitation accepted for processing.'),
    ).toBeVisible();
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    );
    expect(invalidate).toHaveBeenCalledWith({ queryKey: queryKeys.users.all });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: queryKeys.audit.all });
    expect(invalidate).toHaveBeenCalledWith({
      queryKey: queryKeys.dashboard(),
    });
    const reopened = await openInvite('');
    expect(
      within(reopened).getByLabelText('Display name (optional)'),
    ).toHaveValue('');
  });
  it('sends null for an omitted optional name and resets a cancelled form without HTTP', async () => {
    const fetch = mockCommand();
    mount();
    let dialog = await openInvite('first@example.test', 'Do not persist');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    expect(fetch).not.toHaveBeenCalled();
    dialog = await openInvite('second@example.test');
    expect(
      within(dialog).getByLabelText('Display name (optional)'),
    ).toHaveValue('');
    submit(dialog);
    await screen.findByText('Invitation accepted for processing.');
    expect(fetch).toHaveBeenLastCalledWith(
      '/api/v1/admin/users/invitations',
      expect.objectContaining({
        body: JSON.stringify({
          email: 'second@example.test',
          displayName: null,
        }),
      }),
    );
  });
  it('keeps the form and shows a safe duplicate conflict without retry or invalidation', async () => {
    const fetch = mockCommand(() =>
      json(
        { code: 'admin_email_conflict', detail: '<script>secret</script>' },
        409,
      ),
    );
    const { invalidate } = mount();
    const dialog = await openInvite();
    submit(dialog);
    expect(
      await screen.findByText(/An account with this email already exists/),
    ).toBeVisible();
    expect(dialog).toBeVisible();
    expect(within(dialog).getByLabelText('Email')).toHaveValue(
      'invited@example.test',
    );
    expect(screen.queryByText(/<script>/)).not.toBeInTheDocument();
    expect(fetch).toHaveBeenCalledTimes(2);
    expect(invalidate).not.toHaveBeenCalled();
  });
  it('maps server validation fields to safe form messages', async () => {
    mockCommand(() =>
      json(
        {
          code: 'validation_failed',
          errors: { Email: ['unsafe-value'], displayName: ['unsafe-name'] },
        },
        400,
      ),
    );
    mount();
    submit(await openInvite());
    expect(await screen.findByText('Check the email address.')).toBeVisible();
    expect(
      screen.getByText(
        'Use at most 120 characters without control characters.',
      ),
    ).toBeVisible();
    expect(screen.queryByText('unsafe-value')).not.toBeInTheDocument();
  });
  it.each([
    [403, 'forbidden', 'You do not have permission'],
    [400, 'csrf_invalid', 'Your security check expired'],
    [503, 'invitations_unavailable', 'Invitations are currently unavailable'],
    [429, 'rate_limited', 'Too many requests'],
  ])(
    'handles HTTP %i / %s without automatic retry',
    async (status, code, text) => {
      const fetch = mockCommand(() => json({ code }, status));
      const { invalidate } = mount();
      submit(await openInvite());
      expect(await screen.findByText(new RegExp(text))).toBeVisible();
      expect(fetch).toHaveBeenCalledTimes(2);
      expect(invalidate).not.toHaveBeenCalled();
    },
  );
  it('explains an uncertain network outcome and never automatically replays the invitation', async () => {
    const fetch = mockCommand(() =>
      Promise.reject(new TypeError('raw network secret')),
    );
    mount();
    submit(await openInvite());
    expect(
      await screen.findByText(/The result could not be confirmed/),
    ).toBeVisible();
    expect(screen.queryByText(/raw network secret/)).not.toBeInTheDocument();
    expect(fetch).toHaveBeenCalledTimes(2);
  });
  it('requires explicit confirmation for resend, prevents doubles and refreshes detail/audit after 202', async () => {
    let complete: ((response: Response) => void) | undefined;
    const fetch = mockCommand(
      () =>
        new Promise<Response>((resolve) => {
          complete = resolve;
        }),
    );
    const { invalidate } = mount();
    fireEvent.click(screen.getByRole('button', { name: 'Resend invitation' }));
    const dialog = await screen.findByRole('dialog', {
      name: 'Resend invitation?',
    });
    await waitFor(() => expect(dialog).toBeVisible());
    expect(
      within(dialog).getByText(/Previous invitation links will stop working/),
    ).toBeVisible();
    expect(fetch).not.toHaveBeenCalled();
    const button = within(dialog).getByRole('button', {
      name: 'Request another invitation',
    });
    fireEvent.click(button);
    fireEvent.click(button);
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2));
    expect(button).toBeDisabled();
    expect(fetch).toHaveBeenLastCalledWith(
      `/api/v1/admin/users/${id}/invitations/resend`,
      expect.objectContaining({ method: 'POST', body: undefined }),
    );
    await act(async () => {
      complete?.(new Response(null, { status: 202 }));
      await Promise.resolve();
    });
    await screen.findByText('Invitation accepted for processing.');
    expect(invalidate).toHaveBeenCalledWith({
      queryKey: queryKeys.users.detail(id),
    });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: queryKeys.audit.all });
  });
  it('keeps a rejected resend in confirmation with a clear state conflict', async () => {
    const fetch = mockCommand(() => json({ code: 'admin_user_conflict' }, 409));
    const { invalidate } = mount();
    fireEvent.click(screen.getByRole('button', { name: 'Resend invitation' }));
    fireEvent.click(
      await screen.findByRole('button', { name: 'Request another invitation' }),
    );
    await waitFor(() =>
      expect(screen.getByText(/The account state has changed/)).toBeVisible(),
    );
    expect(screen.getByRole('dialog')).toBeVisible();
    expect(fetch).toHaveBeenCalledTimes(2);
    expect(invalidate).not.toHaveBeenCalled();
  });
  it('aborts an in-flight command when its feature unmounts', async () => {
    const fetch = mockCommand(() => new Promise<Response>(() => undefined));
    const view = mount();
    submit(await openInvite());
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2));
    const options = fetch.mock.calls.at(-1)?.[1];
    view.unmount();
    expect(options?.signal?.aborted).toBe(true);
  });
});
