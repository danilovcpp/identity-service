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
import { UsersPage } from './UsersPage';
import { getUsers } from './api';
import { parseUsersParameters, usersApiQuery } from './parameters';

const item = {
  id: 'cd148904-c939-44af-90fe-e1c26b3f0f75',
  email: 'alice@example.test',
  displayName: null,
  status: 'Active',
  emailVerified: true,
  createdAt: '2026-10-19T12:30:00Z',
};
const json = (value: unknown, status = 200) =>
  new Response(JSON.stringify(value), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
function path(input: RequestInfo | URL | undefined) {
  return typeof input === 'string'
    ? input
    : input instanceof URL
      ? input.href
      : (input?.url ?? '');
}
function responses() {
  return vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
    const params = new URL(path(input), 'https://identity.test').searchParams;
    return Promise.resolve(
      json({
        items: [item],
        page: Number(params.get('Page') ?? 1),
        pageSize: Number(params.get('PageSize') ?? 25),
        total: 80,
      }),
    );
  });
}
function Location() {
  const location = useLocation();
  const navigate = useNavigate();
  return (
    <>
      <output aria-label="Location">
        {location.pathname + location.search}
      </output>
      <button
        onClick={() => {
          void navigate(-1);
        }}
      >
        History back
      </button>
      <button
        onClick={() => {
          void navigate(1);
        }}
      >
        History forward
      </button>
      <output aria-label="List return state">
        {JSON.stringify(location.state)}
      </output>
    </>
  );
}
function mount(path = '/admin/users') {
  const client = createQueryClient();
  const view = render(
    <QueryClientProvider client={client}>
      <ConfigProvider theme={{ token: { motion: false } }}>
        <MemoryRouter initialEntries={[path]}>
          <Location />
          <Routes>
            <Route path="/admin/users" element={<UsersPage />} />
            <Route
              path="/admin/users/:id"
              element={<h1>Detail destination</h1>}
            />
          </Routes>
        </MemoryRouter>
      </ConfigProvider>
    </QueryClientProvider>,
  );
  return { ...view, client };
}
const loaded = () => screen.findByText(item.email, { exact: true });
// jsdom has no layout/ResizeObserver. Actual sizing is covered in Chromium.
beforeEach(() =>
  vi.stubGlobal(
    'ResizeObserver',
    class {
      observe() {
        /* no jsdom layout */
      }
      unobserve() {
        /* no jsdom layout */
      }
      disconnect() {
        /* no jsdom layout */
      }
    },
  ),
);
afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe('users catalog', () => {
  it('renders loading without false totals', () => {
    vi.spyOn(globalThis, 'fetch').mockReturnValue(new Promise(() => undefined));
    mount();
    expect(
      screen.getByRole('status', { name: 'Loading users…' }),
    ).toHaveTextContent('Loading users');
    expect(screen.queryByText(/users found/)).not.toBeInTheDocument();
  });
  it('renders the DTO, null fields, local dates with UTC title, safe detail links and full query keys', async () => {
    const fetch = responses();
    const { client } = mount(
      '/admin/users?search=alice&status=Active&emailVerified=false&sortBy=email&sortDirection=asc',
    );
    await loaded();
    expect(screen.getByText('Not provided')).toBeVisible();
    expect(screen.getByText('80 users found')).toBeVisible();
    expect(screen.getByText('Verified', { exact: true })).toBeVisible();
    expect(document.querySelector('time')).toHaveAttribute(
      'title',
      '2026-10-19T12:30:00.000Z',
    );
    const values = parseUsersParameters(
      new URLSearchParams(
        'search=alice&status=Active&emailVerified=false&sortBy=email&sortDirection=asc',
      ),
    );
    expect(
      client.getQueryData(queryKeys.users.list(usersApiQuery(values))),
    ).toBeDefined();
    expect(fetch).toHaveBeenCalledWith(
      '/api/v1/admin/users?Page=1&PageSize=25&Search=alice&Status=Active&EmailVerified=false&SortBy=email&SortDirection=asc',
      expect.objectContaining({
        method: 'GET',
        credentials: 'same-origin',
        signal: expect.any(AbortSignal) as AbortSignal,
      }),
    );
    fireEvent.click(
      screen.getByRole('link', { name: 'View details for ' + item.email }),
    );
    expect(screen.getByLabelText('Location')).toHaveTextContent(
      '/admin/users/' + item.id,
    );
    expect(screen.getByLabelText('Location')).toHaveTextContent('search=alice');
  });
  it('debounces typing 400ms, resets the page and keeps input focus', async () => {
    const fetch = responses();
    mount('/admin/users?page=2');
    await loaded();
    vi.useFakeTimers();
    const search = screen.getByLabelText('Search users');
    search.focus();
    fireEvent.change(search, { target: { value: 'ali' } });
    await act(() => vi.advanceTimersByTimeAsync(250));
    fireEvent.change(search, { target: { value: 'alice' } });
    await act(() => vi.advanceTimersByTimeAsync(399));
    expect(fetch).toHaveBeenCalledTimes(1);
    await act(() => vi.advanceTimersByTimeAsync(1));
    vi.useRealTimers();
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2));
    expect(screen.getByLabelText('Location')).toHaveTextContent(
      '/admin/users?search=alice',
    );
    expect(search).toHaveFocus();
    expect(path(fetch.mock.calls[1]?.[0])).toContain(
      'Page=1&PageSize=25&Search=alice',
    );
  });
  it('restores filters through browser history and cancels pending drafts on back', async () => {
    responses();
    mount('/admin/users?page=2&search=original');
    await loaded();
    fireEvent.click(screen.getByRole('button', { name: 'Reset filters' }));
    await waitFor(() =>
      expect(screen.getByLabelText('Location')).toHaveTextContent(
        /^\/admin\/users$/,
      ),
    );
    fireEvent.change(screen.getByLabelText('Search users'), {
      target: { value: 'discard draft' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'History back' }));
    await waitFor(() =>
      expect(screen.getByLabelText('Search users')).toHaveValue('original'),
    );
    await new Promise((resolve) => setTimeout(resolve, 450));
    expect(screen.getByLabelText('Location')).toHaveTextContent(
      'page=2&search=original',
    );
    fireEvent.click(screen.getByRole('button', { name: 'History forward' }));
    await waitFor(() =>
      expect(screen.getByLabelText('Search users')).toHaveValue(''),
    );
  });
  it('paginates and sorts on the server without reordering the response locally', async () => {
    const fetch = responses();
    mount();
    await loaded();
    fireEvent.click(screen.getByTitle('Next Page'));
    await waitFor(() =>
      expect(path(fetch.mock.lastCall?.[0])).toContain('Page=2'),
    );
    await loaded();
    const heading = screen.getByRole('columnheader', {
      name: /^Email$/,
    });
    fireEvent.click(heading);
    await waitFor(() =>
      expect(path(fetch.mock.lastCall?.[0])).toContain(
        'SortBy=email&SortDirection=asc',
      ),
    );
    expect(screen.getByLabelText('Location')).toHaveTextContent(
      '/admin/users?sortBy=email&sortDirection=asc',
    );
    expect(
      await screen.findByRole('columnheader', { name: /^Email$/ }),
    ).toHaveAttribute('aria-sort', 'ascending');
  });
  it('filters status and email verification on the server, resetting page to one', async () => {
    const fetch = responses();
    mount('/admin/users?page=2');
    await loaded();
    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Status' }));
    fireEvent.click(
      screen.getByText('Disabled', {
        selector: '.ant-select-item-option-content',
      }),
    );
    await waitFor(() =>
      expect(path(fetch.mock.lastCall?.[0])).toContain(
        'Page=1&PageSize=25&Status=Disabled',
      ),
    );
    fireEvent.mouseDown(screen.getByLabelText('Email verification'));
    fireEvent.click(
      screen.getByText('Not verified', {
        selector: '.ant-select-item-option-content',
      }),
    );
    await waitFor(() =>
      expect(path(fetch.mock.lastCall?.[0])).toContain('EmailVerified=false'),
    );
  });
  it('handles an empty page beyond the last result with a first-page action', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) =>
      Promise.resolve(
        json({
          items: [],
          total: 0,
          page: path(input).includes('Page=3') ? 3 : 1,
          pageSize: 25,
        }),
      ),
    );
    mount('/admin/users?page=3');
    expect(
      await screen.findByRole('heading', { name: 'No users found' }),
    ).toBeVisible();
    expect(screen.getByText('0 users found')).toBeVisible();
    fireEvent.click(screen.getByRole('button', { name: 'Go to first page' }));
    await waitFor(() =>
      expect(screen.getByLabelText('Location')).toHaveTextContent(
        /^\/admin\/users$/,
      ),
    );
  });
  it('resets to page one when changing page size', async () => {
    const fetch = responses();
    mount('/admin/users?page=2');
    await loaded();
    fireEvent.mouseDown(
      screen.getByRole('combobox', { name: 'Rows per page' }),
    );
    fireEvent.click(
      screen.getByText('50 / page', {
        selector: '.ant-select-item-option-content',
      }),
    );
    await waitFor(() =>
      expect(path(fetch.mock.lastCall?.[0])).toContain('Page=1&PageSize=50'),
    );
    expect(screen.getByLabelText('Location')).toHaveTextContent(
      '/admin/users?pageSize=50',
    );
  });
  it('preserves a pending search when another filter is changed before debounce', async () => {
    const fetch = responses();
    mount('/admin/users?page=2');
    await loaded();
    fireEvent.change(screen.getByLabelText('Search users'), {
      target: { value: 'alice' },
    });
    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Status' }));
    fireEvent.click(
      screen.getByText('Disabled', {
        selector: '.ant-select-item-option-content',
      }),
    );
    await waitFor(() =>
      expect(path(fetch.mock.lastCall?.[0])).toContain(
        'Page=1&PageSize=25&Search=alice&Status=Disabled',
      ),
    );
    expect(screen.getByLabelText('Search users')).toHaveValue('alice');
  });
  it('accepts safe integer strings from the actual OpenAPI union', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      json({ items: [], page: '1', pageSize: '25', total: '0' }),
    );
    await expect(
      getUsers(
        parseUsersParameters(new URLSearchParams()),
        new AbortController().signal,
      ),
    ).resolves.toEqual({ items: [], page: 1, pageSize: 25, total: 0 });
  });
  it('offers only manual GET retry after failure and hides stale results on a failed refresh', async () => {
    const fetch = responses().mockResolvedValue(
      json({ code: 'unavailable', detail: 'do not disclose' }, 500),
    );
    mount();
    await screen.findByRole('alert');
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(screen.queryByText('do not disclose')).not.toBeInTheDocument();
    fetch.mockResolvedValue(
      json({ items: [item], page: 1, pageSize: 25, total: 1 }),
    );
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    await loaded();
    fetch.mockRejectedValue(new Error('private'));
    fireEvent.click(screen.getByRole('button', { name: 'Refresh' }));
    await screen.findByRole('alert');
    expect(screen.queryByText(item.email)).not.toBeInTheDocument();
  });
  it('escapes user content and projects away unexpected sensitive fields', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      json({
        items: [
          {
            ...item,
            displayName: '<img src=x onerror=alert(1)>',
            passwordHash: 'never-cache',
          },
        ],
        page: 1,
        pageSize: 25,
        total: 1,
        secret: 'never-cache',
      }),
    );
    const { client } = mount();
    await loaded();
    expect(screen.getByText('<img src=x onerror=alert(1)>')).toBeVisible();
    expect(
      within(screen.getByRole('table')).queryByRole('img'),
    ).not.toBeInTheDocument();
    expect(
      JSON.stringify(
        client
          .getQueryCache()
          .getAll()
          .map((query) => query.state.data),
      ),
    ).not.toContain('never-cache');
  });
  it('aborts its pending GET when leaving the route', () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockReturnValue(new Promise(() => undefined));
    const { unmount } = mount();
    const signal = fetch.mock.calls[0]?.[1]?.signal;
    unmount();
    expect(signal?.aborted).toBe(true);
  });
  it.each(['-1', '9007199254740993', null])(
    'rejects malformed or imprecise total %s',
    async (total) => {
      vi.spyOn(globalThis, 'fetch').mockResolvedValue(
        json({ items: [], page: 1, pageSize: 25, total }),
      );
      await expect(
        getUsers(
          parseUsersParameters(new URLSearchParams()),
          new AbortController().signal,
        ),
      ).rejects.toBeInstanceOf(ClientError);
    },
  );
});
