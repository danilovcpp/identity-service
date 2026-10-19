import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import * as authApi from '@/features/auth/api';
import { testProfile } from '@/test/auth';
import * as dashboardApi from '@/features/dashboard/api';
import { testDashboard } from '@/test/dashboard';
import { App } from '@/app/App';
import { MemoryRouter } from 'react-router-dom';
import { AppProviders } from '@/app/AppProviders';

describe('App', () => {
  it('renders the authenticated Identity Console application', async () => {
    vi.spyOn(authApi, 'getSession').mockResolvedValue(testProfile);
    vi.spyOn(dashboardApi, 'getDashboard').mockResolvedValue(testDashboard);
    render(
      <AppProviders>
        <MemoryRouter initialEntries={['/admin']}>
          <App />
        </MemoryRouter>
      </AppProviders>,
    );

    expect(screen.getByRole('main')).toBeInTheDocument();
    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: 'Dashboard',
      }),
    ).toBeVisible();
  });
});
