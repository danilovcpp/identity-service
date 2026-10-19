import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { App } from '@/app/App';
import { MemoryRouter } from 'react-router-dom';
import { AppProviders } from '@/app/AppProviders';

describe('App', () => {
  it('renders the Identity Console application', () => {
    render(
      <AppProviders>
        <MemoryRouter initialEntries={['/admin']}>
          <App />
        </MemoryRouter>
      </AppProviders>,
    );

    expect(screen.getByRole('main')).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { level: 1, name: 'Identity Console' }),
    ).toBeVisible();
  });
});
