import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
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
  it('redirects the public index to the console and does not fetch data or invent a user', () => {
    const fetch = vi.spyOn(globalThis, 'fetch');
    renderApp('/');
    expect(
      screen.getByRole('heading', { level: 1, name: 'Identity Console' }),
    ).toBeVisible();
    expect(screen.getByRole('main')).toHaveAttribute('id', 'main-content');
    expect(screen.queryByText(/Dashboard/)).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: /logout|sign out/i }),
    ).not.toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('only enables the console link and supports desktop collapse/expand', async () => {
    renderApp();
    expect(
      screen.getByRole('navigation', { name: 'Primary navigation' }),
    ).toBeVisible();
    expect(screen.getByRole('menuitem', { name: 'Users' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    expect(screen.getByRole('menuitem', { name: 'Audit log' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    expect(
      screen.queryByRole('link', { name: 'Users' }),
    ).not.toBeInTheDocument();
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
    expect(
      screen.queryByRole('navigation', { name: 'Primary navigation' }),
    ).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Open navigation' }));
    const drawer = await screen.findByRole('dialog');
    expect(
      within(drawer).getByRole('navigation', { name: 'Primary navigation' }),
    ).toBeVisible();
    fireEvent.click(within(drawer).getByRole('link', { name: 'Console' }));
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
    );
    expect(
      screen.getByRole('heading', { name: 'Identity Console' }),
    ).toBeVisible();
  });

  it.each([
    '/missing',
    '/account/activate',
    '/admin/users',
    '/admin/login',
    '/admin/audit',
  ])(
    'shows a real not-found state for the unimplemented route %s',
    async (path) => {
      renderApp(path);
      expect(
        screen.getByRole('heading', { level: 1, name: 'Page not found' }),
      ).toBeVisible();
      expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
      fireEvent.click(screen.getByRole('button', { name: 'Back to console' }));
      await waitFor(() =>
        expect(
          screen.getByRole('heading', { level: 1, name: 'Identity Console' }),
        ).toBeVisible(),
      );
    },
  );
});
