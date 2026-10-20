export type AccountFlow = 'activate' | 'reset-password';
export type LinkState =
  | { token: string; problem?: never }
  | { token?: never; problem: 'missing' | 'invalid' };

// A short-lived handoff from bootstrap to component state. Release after commit
// rather than during render so React StrictMode can initialize the page twice.
export function captureAccountLink(location: Location, history: History) {
  // Match the router's decoded, case-insensitive paths. Keep encoded slashes
  // reserved: they must not introduce new route segments.
  let path: string;
  try {
    path = decodeURI(location.pathname).replace(/\/+$/, '').toLowerCase();
  } catch {
    return undefined;
  }
  if (!['/account/activate', '/account/reset-password'].includes(path))
    return undefined;
  const hash = location.hash;
  if (hash)
    history.replaceState(
      history.state,
      '',
      location.pathname + location.search,
    );
  let value: LinkState | undefined;
  const parameters = new URLSearchParams(hash.slice(1));
  const token = parameters.get('token');
  value = !hash
    ? { problem: 'missing' }
    : parameters.size === 1 && token && /^[A-Za-z0-9_-]{43,512}$/.test(token)
      ? { token }
      : { problem: 'invalid' };
  return {
    read(pathname: string): LinkState {
      return pathname === path && value ? value : { problem: 'missing' };
    },
    release() {
      value = undefined;
    },
  };
}

type Entry = NonNullable<ReturnType<typeof captureAccountLink>>;

export function watchAccountLinks(window: Window) {
  const initial = captureAccountLink(window.location, window.history);
  let receiver: ((entry: Entry) => void) | undefined;
  function receive() {
    if (!window.location.hash) return;
    const entry = captureAccountLink(window.location, window.history);
    if (!entry) return;
    try {
      receiver?.(entry);
    } finally {
      entry.release();
    }
  }
  // Register before BrowserRouter: native fragment navigation fires popstate
  // first. The router must read an already-clean URL, including on link replay.
  window.addEventListener('popstate', receive);
  window.addEventListener('hashchange', receive);
  return {
    read: (path: string): LinkState =>
      initial?.read(path) ?? { problem: 'missing' },
    release: () => initial?.release(),
    subscribe(listener: (entry: Entry) => void) {
      receiver = listener;
      return () => {
        receiver = undefined;
      };
    },
    dispose() {
      receiver = undefined;
      initial?.release();
      window.removeEventListener('popstate', receive);
      window.removeEventListener('hashchange', receive);
    },
  };
}

export type AccountLink = ReturnType<typeof watchAccountLinks>;
