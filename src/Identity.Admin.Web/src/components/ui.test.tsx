import { Button, ConfigProvider } from 'antd';
import type { PropsWithChildren } from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { AppProviders } from '@/app/AppProviders';
import { ApiError, ClientError } from '@/api/errors';
import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { PageHeader } from '@/components/PageHeader';
import { ConfirmAction } from '@/components/ConfirmAction';
import { StatusTag } from '@/components/StatusTag';
import { AppErrorBoundary } from '@/components/AppErrorBoundary';

describe('shared UI', () => {
  it('renders a semantic page heading, breadcrumb and optional actions', () => {
    render(
      <AppProviders>
        <PageHeader
          title="Example page"
          description="A description"
          breadcrumbs={[{ title: 'Console' }]}
          actions={<Button>Example action</Button>}
        />
      </AppProviders>,
    );
    expect(
      screen.getByRole('heading', { level: 1, name: 'Example page' }),
    ).toBeVisible();
    expect(screen.getByText('Console')).toBeVisible();
    expect(
      screen.getByRole('button', { name: 'Example action' }),
    ).toBeVisible();
  });

  it('announces loading and supports a contextual label', () => {
    render(<LoadingState title="Loading accounts" />);
    expect(
      screen.getByRole('status', { name: 'Loading accounts' }),
    ).toHaveAttribute('aria-live', 'polite');
  });

  it('shows an explicit empty state without fabricating data', () => {
    render(
      <EmptyState
        title="No results"
        description="Change your filters."
        action={<Button>Clear filters</Button>}
      />,
    );
    expect(screen.getByRole('region', { name: 'No results' })).toBeVisible();
    expect(screen.getByText('Change your filters.')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Clear filters' })).toBeVisible();
  });

  it('renders a safe error, a reference and a manually invoked retry', () => {
    const retry = vi.fn();
    render(
      <ErrorState
        error={new ApiError(403, { traceId: 'trace-123' })}
        onRetry={retry}
      />,
    );
    expect(screen.getByRole('alert')).toBeVisible();
    expect(
      screen.getByText('You do not have permission to perform this action.'),
    ).toBeVisible();
    expect(screen.getByText('trace-123')).toBeVisible();
    expect(retry).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(retry).toHaveBeenCalledTimes(1);
  });

  it('never renders unknown exception details or arbitrary object fields', () => {
    const view = render(
      <ErrorState error={new Error('private-password-or-token')} />,
    );
    expect(
      screen.queryByText(/private-password-or-token/),
    ).not.toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    view.rerender(<ErrorState error={{ detail: 'raw-secret' }} />);
    expect(screen.queryByText(/raw-secret/)).not.toBeInTheDocument();
    view.rerender(<ErrorState error={new ClientError('network')} />);
    expect(screen.getByText(/Unable to reach Identity/)).toBeVisible();
  });

  it.each([
    ['Active', 'Active'],
    ['PendingVerification', 'Pending verification'],
    ['Invited', 'Invited'],
    ['Disabled', 'Disabled'],
    ['Deactivated', 'Deactivated'],
  ])('labels the real backend status %s', (status, label) => {
    render(<StatusTag status={status} />);
    expect(screen.getByText(label)).toBeVisible();
  });

  it('renders an unfamiliar status as escaped text without inventing a state', () => {
    const status = '<img src=x onerror=alert(1)>';
    const { container } = render(<StatusTag status={status} />);
    expect(screen.getByText(status)).toBeVisible();
    expect(container.querySelector('img')).toBeNull();
  });

  it('only confirms after a deliberate click, and blocks confirmation/cancel while pending', async () => {
    const confirm = vi.fn();
    const cancel = vi.fn();
    const props = {
      open: true,
      title: 'Confirm example',
      description: 'Example consequences',
      confirmText: 'Continue',
      onConfirm: confirm,
      onCancel: cancel,
      danger: true,
    };
    // jsdom cannot finish CSS animations; disable motion only in this test wrapper.
    const wrapper = ({ children }: PropsWithChildren) => (
      <AppProviders>
        <ConfigProvider theme={{ token: { motion: false } }}>
          {children}
        </ConfigProvider>
      </AppProviders>
    );
    const view = render(<ConfirmAction {...props} />, { wrapper });
    await waitFor(() =>
      expect(
        screen.getByRole('dialog', { name: 'Confirm example' }),
      ).toBeVisible(),
    );
    expect(confirm).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
    expect(confirm).toHaveBeenCalledTimes(1);
    view.rerender(<ConfirmAction {...props} pending />);
    expect(screen.getByRole('button', { name: 'Continue' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
    fireEvent.keyDown(screen.getByRole('dialog'), {
      key: 'Escape',
      keyCode: 27,
    });
    expect(confirm).toHaveBeenCalledTimes(1);
    expect(cancel).not.toHaveBeenCalled();
    view.rerender(<ConfirmAction {...props} />);
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(cancel).toHaveBeenCalledTimes(1);
  });

  it('recovers render errors without displaying the exception', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    function Broken(): never {
      throw new Error('private-render-details');
    }
    const view = render(
      <AppProviders>
        <MemoryRouter>
          <AppErrorBoundary>
            <Broken />
          </AppErrorBoundary>
        </MemoryRouter>
      </AppProviders>,
    );
    expect(screen.getByRole('alert')).toBeVisible();
    expect(
      screen.queryByText(/private-render-details/),
    ).not.toBeInTheDocument();
    view.rerender(
      <AppProviders>
        <MemoryRouter>
          <AppErrorBoundary>
            <p>Recovered</p>
          </AppErrorBoundary>
        </MemoryRouter>
      </AppProviders>,
    );
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    await waitFor(() => expect(screen.getByText('Recovered')).toBeVisible());
  });
});
