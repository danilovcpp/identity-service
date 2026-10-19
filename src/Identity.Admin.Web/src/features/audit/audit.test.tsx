import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import { ConfigProvider } from 'antd';
import { QueryClientProvider } from '@tanstack/react-query';
import {
  MemoryRouter,
  Route,
  Routes,
  useLocation,
  useNavigate,
} from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '@/app/queryClient';
import { queryKeys } from '@/api/queryKeys';
import { ClientError } from '@/api/errors';
import { TestAuth } from '@/test/TestAuth';
import {
  testUserDetails as account,
  testUserEvent as event,
} from '@/test/userDetails';
import { AuditPage } from './AuditPage';
import { AuditTable } from './AuditTable';
import { getAuditEvents } from './api';
import { auditApiQuery, parseAuditParameters } from './parameters';

const row = { ...event, actorUserId: account.id };
const json = (value: unknown, status = 200) =>
  new Response(JSON.stringify(value), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
const path = (input: RequestInfo | URL) =>
  typeof input === 'string'
    ? input
    : input instanceof URL
      ? input.href
      : input.url;
const data = (items: unknown[] = [row], page = 1, total = items.length) => ({
  items,
  page,
  pageSize: 25,
  total,
});
function responses(items: unknown[] = [row]) {
  return vi
    .spyOn(globalThis, 'fetch')
    .mockImplementation(() => Promise.resolve(json(data(items))));
}
function Location() {
  const location = useLocation();
  const navigate = useNavigate();
  return (
    <>
      <output aria-label="Location">
        {location.pathname + location.search}
      </output>
      <button onClick={() => void navigate(-1)}>History back</button>
      <button onClick={() => void navigate(1)}>History forward</button>
    </>
  );
}
function mount(
  route = '/admin/audit',
  canWrite = false,
  client = createQueryClient(),
) {
  const view = render(
    <QueryClientProvider client={client}>
      <ConfigProvider theme={{ token: { motion: false } }}>
        <TestAuth canWrite={canWrite}>
          <MemoryRouter initialEntries={[route]}>
            <Location />
            <Routes>
              <Route path="/admin/audit" element={<AuditPage />} />
              <Route
                path="/admin/users/:id"
                element={<h1>Account destination</h1>}
              />
            </Routes>
          </MemoryRouter>
        </TestAuth>
      </ConfigProvider>
    </QueryClientProvider>,
  );
  return { ...view, client };
}
beforeEach(() => {
  vi.stubGlobal(
    'ResizeObserver',
    class {
      observe() {
        /* no layout in jsdom */
      }
      unobserve() {
        /* no layout in jsdom */
      }
      disconnect() {
        /* no layout in jsdom */
      }
    },
  );
});
afterEach(() => vi.unstubAllGlobals());
describe('audit log', () => {
  it('keeps the scoped activity array separate from a paginated audit response with identical HTTP filters', async () => {
    const client = createQueryClient();
    const activity = [
      { id: row.id, eventType: row.eventType, occurredAt: row.occurredAt },
    ];
    client.setQueryData(queryKeys.audit.userActivity(account.id), activity);
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      json({ ...data(), pageSize: 10 }),
    );
    const parameters = new URLSearchParams({
      pageSize: '10',
      userId: account.id,
    });
    mount('/admin/audit?' + parameters.toString(), false, client);
    expect(await screen.findByText('verify_email')).toBeVisible();
    expect(
      client.getQueryData(queryKeys.audit.userActivity(account.id)),
    ).toEqual(activity);
    expect(
      client.getQueryData(
        queryKeys.audit.list(auditApiQuery(parseAuditParameters(parameters))),
      ),
    ).toMatchObject({ items: [{ id: row.id }], pageSize: 10 });
  });
  it.each([false, true])(
    'reads real-contract fields without write controls for canWrite=%s',
    async (canWrite) => {
      const fetch = responses();
      mount('/admin/audit', canWrite);
      expect(await screen.findByText('Email verified')).toBeVisible();
      expect(screen.getByText('verify_email')).toBeVisible();
      expect(screen.getByText(event.correlationId)).toBeVisible();
      expect(document.querySelector('time')).toHaveAttribute(
        'title',
        '2026-10-19T12:30:00.000Z',
      );
      expect(
        screen.getAllByRole('link', { name: `View user ${account.id}` }),
      ).toHaveLength(2);
      expect(
        screen.queryByRole('button', {
          name: /delete|export|edit|revoke|invite/i,
        }),
      ).not.toBeInTheDocument();
      expect(fetch.mock.calls.every(([, init]) => init?.method === 'GET')).toBe(
        true,
      );
    },
  );
  it('shows loading, then zero events without an empty wide table', async () => {
    let complete: ((response: Response) => void) | undefined;
    vi.spyOn(globalThis, 'fetch').mockImplementation(
      () =>
        new Promise<Response>((resolve) => {
          complete = resolve;
        }),
    );
    mount();
    expect(
      screen.getByRole('status', { name: 'Loading audit events…' }),
    ).toBeVisible();
    await act(async () => {
      complete?.(json(data([])));
      await Promise.resolve();
    });
    expect(
      await screen.findByRole('heading', { name: 'No audit events found' }),
    ).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });
  it('validates IDs, event codes and ranges before changing the URL or fetching', async () => {
    const fetch = responses();
    mount();
    await screen.findByText('verify_email');
    fireEvent.change(screen.getByLabelText('Actor user ID'), {
      target: { value: 'invalid' },
    });
    fireEvent.change(screen.getByLabelText('Event type'), {
      target: { value: '<script>' },
    });
    fireEvent.change(screen.getByLabelText('From · local time (inclusive)'), {
      target: { value: '2026-01-02T00:00:00' },
    });
    fireEvent.change(screen.getByLabelText('To · local time (exclusive)'), {
      target: { value: '2026-01-01T00:00:00' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Apply filters' }));
    expect(await screen.findByText('Enter a nonempty UUID.')).toBeVisible();
    expect(
      await screen.findByText('To must be later than from.'),
    ).toBeVisible();
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(screen.getByLabelText('Location')).toHaveTextContent('/admin/audit');
  });
  it('applies every server filter, preserves URL history and resets filters', async () => {
    const fetch = responses();
    mount();
    await screen.findByText('verify_email');
    fireEvent.change(screen.getByLabelText('Actor user ID'), {
      target: { value: account.id },
    });
    fireEvent.change(screen.getByLabelText('Target / related user ID'), {
      target: { value: account.id },
    });
    fireEvent.change(screen.getByLabelText('Event type'), {
      target: { value: 'admin_disabled_user' },
    });
    fireEvent.change(screen.getByLabelText('From · local time (inclusive)'), {
      target: { value: '2026-01-01T00:00:00' },
    });
    fireEvent.change(screen.getByLabelText('To · local time (exclusive)'), {
      target: { value: '2026-01-02T00:00:00' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Apply filters' }));
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2));
    const last = new URL(
      path(fetch.mock.calls.at(-1)![0]),
      'https://identity.test',
    ).searchParams;
    expect(last.get('ActorUserId')).toBe(account.id);
    expect(last.get('UserId')).toBe(account.id);
    expect(last.get('EventType')).toBe('admin_disabled_user');
    expect(last.get('From')).toMatch(/Z$/);
    expect(last.get('To')).toMatch(/Z$/);
    expect(last.get('Page')).toBe('1');
    fireEvent.click(screen.getByRole('button', { name: 'History back' }));
    await waitFor(() =>
      expect(screen.getByLabelText('Event type')).toHaveValue(''),
    );
    fireEvent.click(screen.getByRole('button', { name: 'History forward' }));
    await waitFor(() =>
      expect(screen.getByLabelText('Event type')).toHaveValue(
        'admin_disabled_user',
      ),
    );
    fireEvent.click(screen.getByRole('button', { name: 'Reset filters' }));
    await waitFor(() =>
      expect(screen.getByLabelText('Location')).toHaveTextContent(
        /^\/admin\/audit$/,
      ),
    );
  });
  it('uses server pagination without sorting the current page', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      const page = Number(
        new URL(path(input), 'https://identity.test').searchParams.get('Page'),
      );
      return Promise.resolve(
        json(data([{ ...row, correlationId: `page-${page}` }], page, 26)),
      );
    });
    mount();
    await screen.findByText('page-1');
    fireEvent.click(within(screen.getByTitle('Next Page')).getByRole('button'));
    await screen.findByText('page-2');
    expect(screen.getByLabelText('Location')).toHaveTextContent('page=2');
    expect(fetch).toHaveBeenLastCalledWith(
      '/api/v1/admin/audit-events?Page=2&PageSize=25',
      expect.objectContaining({ credentials: 'same-origin' }),
    );
  });
  it('retries failed GET manually and never renders raw server errors', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation(() =>
        Promise.resolve(json({ detail: 'private-marker' }, 500)),
      );
    mount();
    await screen.findByRole('alert');
    expect(document.body).not.toHaveTextContent('private-marker');
    fetch.mockImplementation(() => Promise.resolve(json(data())));
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(await screen.findByText('verify_email')).toBeVisible();
  });
  it('escapes unknown events/correlation IDs, preserves server order and omits unexpected metadata', async () => {
    const first = {
      ...row,
      eventType: '<img src=x onerror=alert(1)>',
      correlationId: '<script>correlation</script>',
      metadata: { password: 'private-marker' },
      token: 'private-marker',
    };
    responses([
      first,
      { ...row, id: account.id, occurredAt: '2026-01-01T00:00:00Z' },
    ]);
    const { client } = mount();
    expect(await screen.findByText(first.eventType)).toBeVisible();
    expect(screen.getByText(first.correlationId)).toBeVisible();
    expect(document.querySelector('script, img[src=x]')).toBeNull();
    const key = queryKeys.audit.list(
      auditApiQuery(parseAuditParameters(new URLSearchParams())),
    );
    expect(JSON.stringify(client.getQueryData(key))).not.toContain(
      'private-marker',
    );
    const rows = within(screen.getByRole('table')).getAllByRole('row');
    expect(rows[1]).toHaveTextContent(first.eventType);
    expect(rows[2]).toHaveTextContent('verify_email');
  });
  it('renders explicit nulls, a legacy user link, and no link for invalid IDs', () => {
    render(
      <MemoryRouter>
        <AuditTable
          items={[
            {
              ...row,
              actorUserId: null,
              targetUserId: null,
              userId: account.id,
              correlationId: '',
            },
            {
              ...row,
              id: account.id,
              actorUserId: 'invalid',
              targetUserId: null,
              userId: null,
            },
          ]}
        />
      </MemoryRouter>,
    );
    expect(screen.getAllByText('Not recorded').length).toBeGreaterThan(0);
    expect(screen.getByText('Legacy related user')).toBeVisible();
    expect(screen.getAllByRole('link')).toHaveLength(1);
    expect(screen.getByRole('link')).toHaveAttribute(
      'href',
      '/admin/users/' + account.id,
    );
  });
  it.each([
    { ...data(), page: 2 },
    { ...data(), total: '9007199254740992' },
    data([{ ...row, occurredAt: 'invalid' }]),
    data([{ ...row, actorUserId: 'invalid' }]),
    data([row, row]),
  ])('rejects malformed DTOs', async (value) => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(value));
    await expect(
      getAuditEvents(
        parseAuditParameters(new URLSearchParams()),
        new AbortController().signal,
      ),
    ).rejects.toBeInstanceOf(ClientError);
  });
  it('aborts a pending read on unmount', () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockReturnValue(new Promise(() => undefined));
    const view = mount();
    const signal = fetch.mock.calls[0]?.[1]?.signal;
    view.unmount();
    expect(signal?.aborted).toBe(true);
  });
});
