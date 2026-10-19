import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as authApi from '@/features/auth/api';
import { testProfile } from '@/test/auth';
import * as dashboardApi from '@/features/dashboard/api';
import { testDashboard } from '@/test/dashboard';
import { App } from '@/app/App';
import { AppProviders } from '@/app/AppProviders';
import { setTestViewport } from '@/test/viewport';

function renderApp(path = '/admin') {
  return render(
    <AppProviders>
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>
    </AppProviders>,
  );
}

describe('console shell and routes', () => {
  beforeEach(() => {
    vi.spyOn(authApi, 'getSession').mockResolvedValue(testProfile);
    vi.spyOn(dashboardApi, 'getDashboard').mockResolvedValue(testDashboard);
  });
  it('redirects the public index to the authenticated console without business data requests', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch');
    renderApp('/');
    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: 'Dashboard',
      }),
    ).toBeVisible();
    expect(screen.getByRole('main')).toHaveAttribute('id', 'main-content');
    expect(screen.getByRole('link', { name: 'Dashboard' })).toBeVisible();
    expect(
      screen.queryByRole('button', { name: /logout|sign out/i }),
    ).toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('enables Dashboard and Users and supports desktop collapse/expand', async () => {
    renderApp();
    await screen.findByRole('heading', { name: 'Dashboard' });
    expect(
      screen.getByRole('navigation', { name: 'Primary navigation' }),
    ).toBeVisible();
    expect(screen.getByRole('link', { name: 'Users' })).toBeVisible();
    expect(screen.getByRole('menuitem', { name: 'Audit log' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    fireEvent.click(
      screen.getByRole('button', { name: 'Collapse navigation' }),
    );
    expect(
      screen.getByRole('button', { name: 'Expand navigation' }),
    ).toHaveAttribute('aria-expanded', 'false');
    fireEvent.click(screen.getByRole('button', { name: 'Expand navigation' }));
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Collapse navigation' }),
      ).toHaveAttribute('aria-expanded', 'true'),
    );
  });

  it('opens mobile navigation and closes it when navigating to the console', async () => {
    setTestViewport(360);
    renderApp('/admin/missing');
    await screen.findByRole('heading', { name: 'Page not found' });
    expect(
      screen.queryByRole('navigation', { name: 'Primary navigation' }),
    ).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Open navigation' }));
    const drawer = await screen.findByRole('dialog');
    expect(
      within(drawer).getByRole('navigation', { name: 'Primary navigation' }),
    ).toBeVisible();
    fireEvent.click(within(drawer).getByRole('link', { name: 'Dashboard' }));
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    );
    expect(screen.getByRole('heading', { name: 'Dashboard' })).toBeVisible();
  });

  it.each([
    '/missing',
    '/account/activate',
    '/admin/users/missing',
    '/admin/audit',
  ])(
    'shows a real not-found state for the unimplemented route %s',
    async (path) => {
      renderApp(path);
      expect(
        await screen.findByRole('heading', {
          level: 1,
          name: 'Page not found',
        }),
      ).toBeVisible();
      expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
      fireEvent.click(screen.getByRole('button', { name: 'Back to console' }));
      await waitFor(() =>
        expect(
          screen.getByRole('heading', { level: 1, name: 'Dashboard' }),
        ).toBeVisible(),
      );
    },
  );
});
