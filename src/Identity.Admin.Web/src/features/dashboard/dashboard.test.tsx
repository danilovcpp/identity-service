import { QueryClientProvider } from '@tanstack/react-query';
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import { ConfigProvider } from 'antd';
import { describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '@/app/queryClient';
import { ClientError } from '@/api/errors';
import { testDashboard } from '@/test/dashboard';
import { DashboardPage } from './DashboardPage';
import { getDashboard } from './api';

const json = (data: unknown, status = 200) =>
  new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
function mount() {
  const client = createQueryClient();
  const view = render(
    <QueryClientProvider client={client}>
      <ConfigProvider theme={{ token: { motion: false } }}>
        <DashboardPage />
      </ConfigProvider>
    </QueryClientProvider>,
  );
  return { ...view, client };
}

describe('dashboard', () => {
  it('shows loading without fabricated metrics', () => {
    vi.spyOn(globalThis, 'fetch').mockReturnValue(
      new Promise<Response>(() => undefined),
    );
    mount();
    expect(screen.getByRole('status')).toHaveTextContent('Loading dashboard');
    expect(
      screen.queryByRole('region', { name: 'Total users' }),
    ).not.toBeInTheDocument();
  });

  it('renders all seven real counts, UTC period, snapshot and daily values', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation(() => Promise.resolve(json(testDashboard)));
    mount();
    const values: [string, string][] = [
      ['Total users', '1,234'],
      ['Active users', '1,000'],
      ['Active sessions', '42'],
      ['Pending verification', '100'],
      ['Invited', '50'],
      ['Disabled', '60'],
      ['Deactivated', '24'],
    ];
    for (const [label, value] of values) {
      const region = await screen.findByRole('region', { name: label });
      expect(within(region).getByText(value, { exact: true })).toBeVisible();
    }
    expect(
      screen.getByText(/Sep 20, 2026 – Oct 19, 2026 \(UTC\)/),
    ).toBeVisible();
    expect(screen.getByText(/Today is partial/)).toBeVisible();
    expect(screen.getByText(/excludes console sessions/)).toBeVisible();
    const snapshot = document.querySelector(
      'time[datetime="2026-10-19T12:30:00Z"]',
    );
    expect(snapshot).toHaveAttribute('title', '2026-10-19T12:30:00.000Z');
    fireEvent.click(screen.getByText('View daily values'));
    expect(within(screen.getByRole('table')).getAllByRole('row')).toHaveLength(
      31,
    );
    expect(fetch).toHaveBeenCalledWith(
      '/api/v1/admin/dashboard',
      expect.objectContaining({
        method: 'GET',
        credentials: 'same-origin',
        signal: expect.any(AbortSignal) as AbortSignal,
      }),
    );
  });

  it('displays legitimate zero counts and an empty-period message', async () => {
    const empty = {
      ...testDashboard,
      totalUsers: 0,
      activeUsers: 0,
      activeSessions: 0,
      pendingVerificationUsers: 0,
      invitedUsers: 0,
      disabledUsers: 0,
      deactivatedUsers: 0,
      registrationTrend: testDashboard.registrationTrend.map((point) => ({
        ...point,
        count: 0,
      })),
    };
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(empty));
    mount();
    await screen.findByText(/No users yet/);
    expect(screen.getByText('No registrations in this period.')).toBeVisible();
    expect(
      within(screen.getByRole('region', { name: 'Total users' })).getByText(
        '0',
        { exact: true },
      ),
    ).toBeVisible();
    expect(
      screen.queryByRole('button', { name: /invite|create/i }),
    ).not.toBeInTheDocument();
  });

  it('an absent series is distinct from thirty zero registrations', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      json({ ...testDashboard, registrationTrend: [] }),
    );
    mount();
    await screen.findByRole('heading', {
      name: 'Registration data unavailable',
    });
    expect(
      screen.queryByText('No registrations in this period.'),
    ).not.toBeInTheDocument();
    expect(screen.queryByText('View daily values')).not.toBeInTheDocument();
  });

  it('a failed GET has no automatic retry or zero fallback and supports manual retry', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(json({ detail: 'sensitive diagnostics' }, 503));
    mount();
    await screen.findByRole('alert');
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(
      screen.queryByRole('region', { name: 'Total users' }),
    ).not.toBeInTheDocument();
    expect(screen.queryByText('sensitive diagnostics')).not.toBeInTheDocument();
    fetch.mockImplementation(() => Promise.resolve(json(testDashboard)));
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    await screen.findByRole('region', { name: 'Total users' });
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('refresh updates the snapshot from the server and disables duplicate refreshes', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(json(testDashboard));
    mount();
    const refresh = await screen.findByRole('button', {
      name: 'Refresh dashboard',
    });
    let resolve: ((value: Response) => void) | undefined;
    fetch.mockImplementation(
      () =>
        new Promise<Response>((done) => {
          resolve = done;
        }),
    );
    fireEvent.click(refresh);
    await waitFor(() => expect(refresh).toBeDisabled());
    expect(screen.getByText('Refreshing dashboard…')).toBeVisible();
    await act(async () => {
      resolve?.(json({ ...testDashboard, totalUsers: 1235 }));
      await Promise.resolve();
    });
    await waitFor(() =>
      expect(
        within(screen.getByRole('region', { name: 'Total users' })).getByText(
          '1,235',
          { exact: true },
        ),
      ).toBeVisible(),
    );
  });

  it('a failed refresh does not present stale numbers as a current snapshot', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(json(testDashboard));
    mount();
    const refresh = await screen.findByRole('button', {
      name: 'Refresh dashboard',
    });
    fetch.mockRejectedValue(new Error('raw transport details'));
    fireEvent.click(refresh);
    await screen.findByRole('alert');
    expect(
      screen.queryByRole('region', { name: 'Dashboard snapshot' }),
    ).not.toBeInTheDocument();
    expect(screen.queryByText('raw transport details')).not.toBeInTheDocument();
  });

  it('cancels a dashboard read when leaving the page', async () => {
    let signal: AbortSignal | null | undefined;
    vi.spyOn(globalThis, 'fetch').mockImplementation((_input, init) => {
      signal = init?.signal;
      return new Promise<Response>(() => undefined);
    });
    const view = mount();
    await waitFor(() => expect(signal).toBeDefined());
    view.unmount();
    expect(signal?.aborted).toBe(true);
  });

  it('accepts numeric strings declared by OpenAPI and projects only dashboard fields', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      json({
        ...testDashboard,
        totalUsers: '1234',
        unexpected: 'secret',
        registrationTrend: testDashboard.registrationTrend.map((point) => ({
          ...point,
          count: String(point.count),
        })),
      }),
    );
    const data = await getDashboard(new AbortController().signal);
    expect(data.totalUsers).toBe(1234);
    expect(data.registrationTrend[29]?.count).toBe(12);
    expect(data).not.toHaveProperty('unexpected');
  });

  it.each([-1, 0.5, null, true, '', 'NaN', '9007199254740993'])(
    'rejects invalid or imprecise count %s without manufacturing zero',
    async (value) => {
      vi.spyOn(globalThis, 'fetch').mockResolvedValue(
        json({ ...testDashboard, totalUsers: value }),
      );
      await expect(
        getDashboard(new AbortController().signal),
      ).rejects.toBeInstanceOf(ClientError);
    },
  );

  it.each([
    { asOf: 'not-a-date' },
    { registrationTrend: [{ date: '2026-02-31', count: 1 }] },
  ])('rejects invalid date metadata', async (override) => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      json({ ...testDashboard, ...override }),
    );
    await expect(
      getDashboard(new AbortController().signal),
    ).rejects.toBeInstanceOf(ClientError);
  });
});
