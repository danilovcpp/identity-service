import { useQueryClient, type QueryClient } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { useEffect } from 'react';
import { describe, expect, it } from 'vitest';
import { AppProviders } from '@/app/AppProviders';
import { App } from '@/app/App';
import { queryKeys } from '@/api/queryKeys';

function Probe({ capture }: { capture: (client: QueryClient) => void }) {
  const client = useQueryClient();
  useEffect(() => capture(client), [capture, client]);
  return <App />;
}

describe('AppProviders', () => {
  it('keeps the client stable on rerender and creates a fresh client on remount', () => {
    const clients: QueryClient[] = [];
    const capture = (client: QueryClient) => {
      clients.push(client);
    };
    const view = render(
      <AppProviders>
        <Probe capture={capture} />
      </AppProviders>,
    );
    expect(
      screen.getByRole('heading', { name: 'Identity Console' }),
    ).toBeVisible();
    const first = clients[0];
    expect(first).toBeDefined();
    first?.setQueryData(queryKeys.auth.me(), { id: 'old-session' });
    view.rerender(
      <AppProviders>
        <Probe capture={(client) => capture(client)} />
      </AppProviders>,
    );
    expect(clients[1]).toBe(first);
    view.unmount();
    const next = render(
      <AppProviders>
        <Probe capture={capture} />
      </AppProviders>,
    );
    const second = clients[2];
    expect(second).toBeDefined();
    expect(second).not.toBe(first);
    expect(second?.getQueryData(queryKeys.auth.me())).toBeUndefined();
    next.unmount();
    clients.forEach((client) => client.clear());
  });
});
