import { StrictMode } from 'react';
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react';
import { QueryClientProvider } from '@tanstack/react-query';
import { ConfigProvider } from 'antd';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { App } from '@/app/App';
import { createQueryClient } from '@/app/queryClient';
import { captureAccountLink, watchAccountLinks } from './accountLink';
import { passwordProblem } from './passwordPolicy';

const token = 'T'.repeat(43);
const password = 'A test password with spaces 42';
const path = '/account/activate';
const dispose: Array<() => void> = [];
const json = (value: unknown, status = 400) =>
  new Response(JSON.stringify(value), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });

function mount(route = path, fragment = '#token=' + token) {
  window.history.replaceState({ index: 7 }, '', route + fragment);
  const entry = watchAccountLinks(window);
  dispose.push(() => entry.dispose());
  const client = createQueryClient();
  const rendered = render(
    <StrictMode>
      <QueryClientProvider client={client}>
        <ConfigProvider theme={{ token: { motion: false } }}>
          <MemoryRouter initialEntries={[route]}>
            <App initialAccountLink={entry} />
          </MemoryRouter>
        </ConfigProvider>
      </QueryClientProvider>
    </StrictMode>,
  );
  return { ...rendered, client, entry };
}

function fill(first = password, confirmation = first) {
  fireEvent.change(screen.getByLabelText('New password'), {
    target: { value: first },
  });
  fireEvent.change(screen.getByLabelText('Confirm password'), {
    target: { value: confirmation },
  });
}
function submit(name = 'Activate account') {
  fireEvent.click(screen.getByRole('button', { name }));
}

afterEach(() => {
  dispose.splice(0).forEach((cleanup) => cleanup());
  window.history.replaceState(null, '', '/');
});

describe('public account link handling', () => {
  it.each(['/ACCOUNT/ACTIVATE/', '/%61ccount/activate/'])(
    'cleans fragments on route variants accepted by the router: %s',
    (route) => {
      window.history.replaceState(null, '', route + '#token=' + token);
      const entry = captureAccountLink(window.location, window.history);
      expect(window.location.hash).toBe('');
      expect(entry?.read(path)).toEqual({ token });
    },
  );
  it('clears the fragment synchronously, preserves history state/search and releases the bootstrap handoff', () => {
    window.history.replaceState(
      { index: 9 },
      '',
      path + '?lang=en#token=' + token,
    );
    const entry = captureAccountLink(window.location, window.history);
    expect(window.location.hash).toBe('');
    expect(window.location.pathname + window.location.search).toBe(
      path + '?lang=en',
    );
    expect(window.history.state).toEqual({ index: 9 });
    expect(entry?.read(path)).toEqual({ token });
    entry?.release();
    expect(entry?.read(path)).toEqual({ problem: 'missing' });
  });
  it.each([
    '#token=short',
    '#token=' + token + '&token=' + token,
    '#token=' + token + '&extra=x',
    '#broken',
    '#token=%ZZ',
  ])('rejects and clears malformed fragments (%#)', (fragment) => {
    window.history.replaceState(null, '', path + fragment);
    const entry = captureAccountLink(window.location, window.history);
    expect(window.location.hash).toBe('');
    expect(entry?.read(path)).toEqual({ problem: 'invalid' });
  });
  it('does not accept query tokens or pass an activation token to reset', () => {
    window.history.replaceState(null, '', path + '?token=' + token);
    expect(
      captureAccountLink(window.location, window.history)?.read(path),
    ).toEqual({ problem: 'missing' });
    window.history.replaceState(null, '', path + '#token=' + token);
    expect(
      captureAccountLink(window.location, window.history)?.read(
        '/account/reset-password',
      ),
    ).toEqual({ problem: 'missing' });
  });
});

describe('public password forms', () => {
  it('handles a new native fragment before router observers and clears a previous success state', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response(null, { status: 204 }));
    const { entry } = mount();
    fill();
    submit();
    await screen.findByText('Account activated');
    let observedHash: string | undefined;
    const observe = () => {
      observedHash = window.location.hash;
    };
    window.addEventListener('popstate', observe, { once: true });
    act(() => {
      window.history.replaceState(null, '', path + '#token=' + 'Z'.repeat(43));
      window.dispatchEvent(new PopStateEvent('popstate'));
    });
    expect(observedHash).toBe('');
    expect(entry.read(path)).toEqual({ problem: 'missing' });
    expect(screen.getByLabelText('New password')).toHaveValue('');
    expect(screen.queryByText('Account activated')).not.toBeInTheDocument();
    fill();
    submit();
    await screen.findByText('Account activated');
    expect(fetch).toHaveBeenCalledTimes(2);
  });
  it.each(['/account/activate', '/account/reset-password'])(
    'is anonymous, has an explicit missing-link state and makes no session requests: %s',
    (route) => {
      const fetch = vi.spyOn(globalThis, 'fetch');
      mount(route, '');
      expect(screen.getByText('Link required')).toBeVisible();
      expect(screen.queryByLabelText('New password')).not.toBeInTheDocument();
      expect(fetch).not.toHaveBeenCalled();
    },
  );
  it.each([
    [
      path,
      'Activate account',
      '/api/v1/Auth/invitations/accept',
      'Account activated',
    ],
    [
      '/account/reset-password',
      'Reset password',
      '/api/v1/Auth/password/reset',
      'Password changed',
    ],
  ])(
    'submits only JSON and clears secrets on success under StrictMode: %s',
    async (route, action, endpoint, title) => {
      const fetch = vi
        .spyOn(globalThis, 'fetch')
        .mockResolvedValue(new Response(null, { status: 204 }));
      const { client, entry } = mount(route);
      expect(window.location.hash).toBe('');
      expect(entry?.read(route)).toEqual({ problem: 'missing' });
      expect(fetch).not.toHaveBeenCalled();
      fill();
      submit(action);
      await screen.findByText(title);
      expect(fetch).toHaveBeenCalledTimes(1);
      expect(fetch).toHaveBeenCalledWith(
        endpoint,
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ token, newPassword: password }),
          headers: {
            Accept: 'application/json',
            'Content-Type': 'application/json',
          },
          cache: 'no-store',
          redirect: 'error',
        }),
      );
      expect(
        screen.getByRole('link', { name: 'Administrator sign in' }),
      ).toHaveAttribute('href', '/admin/login');
      expect(screen.queryByLabelText('New password')).not.toBeInTheDocument();
      expect(client.getMutationCache().getAll()).toHaveLength(0);
      expect(client.getQueryCache().getAll()).toHaveLength(0);
      expect(localStorage.length + sessionStorage.length).toBe(0);
      expect(document.body.textContent).not.toContain(token);
      expect(document.body.textContent).not.toContain(password);
    },
  );
  it('rejects weak passwords and mismatched confirmation before transport', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch');
    mount();
    fill('short');
    submit();
    await screen.findByText(
      'Use 15–128 Unicode characters after normalization.',
    );
    fill(password, 'another password');
    submit();
    await screen.findByText('The passwords do not match.');
    expect(fetch).not.toHaveBeenCalled();
  });
  it.each(['invalid_invitation_token', 'invalid_password_reset_token'])(
    'shows a generic unavailable state for expired, consumed or rejected links: %s',
    async (code) => {
      vi.spyOn(globalThis, 'fetch').mockResolvedValue(
        json({ code, detail: token }),
      );
      mount();
      fill();
      submit();
      await screen.findByText('Link unavailable');
      expect(screen.queryByLabelText('New password')).not.toBeInTheDocument();
      expect(document.body.textContent).not.toContain(token);
    },
  );
  it('clears passwords after server policy rejection and permits a corrected submission without a new link', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(
        json({ code: 'validation_failed', errors: { newPassword: [token] } }),
      )
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    mount();
    fill('correcthorsebatterystaple');
    submit();
    await screen.findByText(/choose a less common password/);
    expect(screen.getByLabelText('New password')).toHaveValue('');
    expect(screen.getByLabelText('Confirm password')).toHaveValue('');
    expect(document.body.textContent).not.toContain(token);
    fill();
    submit();
    await screen.findByText('Account activated');
    expect(fetch).toHaveBeenCalledTimes(2);
  });
  it('guards duplicate submission and cancels transport on unmount', async () => {
    let signal: AbortSignal | null | undefined;
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation((_input, init) => {
        signal = init?.signal;
        return new Promise<Response>(() => undefined);
      });
    const { unmount } = mount();
    fill();
    submit();
    submit();
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));
    expect(
      screen.getByRole('button', { name: 'Activate account' }),
    ).toBeDisabled();
    unmount();
    expect(signal?.aborted).toBe(true);
  });
  it('does not retry uncertain network results and uses safe feedback', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockRejectedValue(new Error(token));
    mount();
    fill();
    submit();
    await screen.findByText(/The result could not be confirmed/);
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(screen.getByLabelText('New password')).toHaveValue('');
    expect(document.body.textContent).not.toContain(token);
  });
  it('clears the pending form after timeout without an automatic retry', async () => {
    const timeout = new AbortController();
    vi.spyOn(AbortSignal, 'timeout').mockReturnValue(timeout.signal);
    const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation(
      (_input, init) =>
        new Promise<Response>((_resolve, reject) => {
          init?.signal?.addEventListener(
            'abort',
            () => reject(new Error('Aborted')),
            { once: true },
          );
        }),
    );
    mount();
    fill();
    submit();
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));
    act(() => {
      timeout.abort(new DOMException('Timeout', 'TimeoutError'));
    });
    expect(
      await screen.findByText(/The result could not be confirmed/),
    ).toBeVisible();
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(screen.getByLabelText('New password')).toHaveValue('');
  });
});

describe('password policy compatibility', () => {
  it.each(['a'.repeat(15), '😀'.repeat(15), 'a'.repeat(128), ' '.repeat(15)])(
    'accepts Unicode characters/spaces without composition rules (%#)',
    (value) => expect(passwordProblem(value)).toBeUndefined(),
  );
  it.each([
    'short',
    'a'.repeat(129),
    '\ud800'.repeat(15),
    '㍍'.repeat(100),
    ' '.repeat(1025),
    '각'.repeat(128),
  ])('rejects length/Unicode/normalized and raw input limits (%#)', (value) =>
    expect(passwordProblem(value)).toBeDefined(),
  );
});
