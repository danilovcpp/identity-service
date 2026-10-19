import type {
  ApiMethod,
  ApiPath,
  ApiResponse,
  RequestOptions,
} from '@/api/contracts';
import {
  ApiError,
  cancellationError,
  ClientError,
  isRecord,
} from '@/api/errors';
import type { components } from '@/api/schema';

type Fetch = typeof globalThis.fetch;
type CsrfFlight = {
  controller: AbortController;
  promise: Promise<string>;
  waiters: number;
};

// One caller cancelling must not cancel another caller's shared CSRF acquisition.
function waitWithSignal<T>(
  promise: Promise<T>,
  signal?: AbortSignal,
): Promise<T> {
  if (!signal) return promise;
  if (signal.aborted) return Promise.reject(cancellationError(signal));
  return new Promise<T>((resolve, reject) => {
    const onAbort = () => reject(cancellationError(signal));
    signal.addEventListener('abort', onAbort, { once: true });
    void promise.then(
      (value) => {
        signal.removeEventListener('abort', onAbort);
        resolve(value);
      },
      (error: unknown) => {
        signal.removeEventListener('abort', onAbort);
        reject(error instanceof Error ? error : new ClientError('network'));
      },
    );
  });
}

function requestUrl(template: string, path: unknown, query: unknown): string {
  const parameters = isRecord(path) ? path : {};
  const pathname = template.replace(/\{([^}]+)\}/g, (_, name: string) => {
    const value = parameters[name];
    if (
      typeof value !== 'string' ||
      !value ||
      value === '.' ||
      value === '..'
    ) {
      throw new ClientError('invalid_request');
    }
    return encodeURIComponent(value);
  });
  if (!pathname.startsWith('/api/v1/') || /[?#\\]/.test(pathname)) {
    throw new ClientError('invalid_request');
  }
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(isRecord(query) ? query : {})) {
    if (value === undefined || value === null) continue;
    if (
      typeof value !== 'string' &&
      typeof value !== 'number' &&
      typeof value !== 'boolean'
    ) {
      throw new ClientError('invalid_request');
    }
    search.set(key, String(value));
  }
  return search.size ? `${pathname}?${search.toString()}` : pathname;
}

export function createApiClient(
  fetch: Fetch = (input, init) => globalThis.fetch(input, init),
) {
  let csrfToken: string | undefined;
  let csrfFlight: CsrfFlight | undefined;

  function clearCsrfToken() {
    csrfToken = undefined;
    const previous = csrfFlight;
    csrfFlight = undefined;
    previous?.controller.abort();
  }

  async function send(url: string, init: RequestInit): Promise<unknown> {
    const signal = init.signal;
    try {
      const response = await fetch(url, {
        ...init,
        credentials: 'same-origin',
        cache: 'no-store',
        redirect: 'error',
      });
      if (response.status === 204 && response.ok) return undefined;
      const text = await response.text();
      let data: unknown;
      const json = /\bapplication\/(?:[\w.-]+\+)?json\b/i.test(
        response.headers.get('Content-Type') ?? '',
      );
      if (text && json) {
        try {
          data = JSON.parse(text) as unknown;
        } catch {
          if (response.ok) throw new ClientError('invalid_response');
        }
      }
      if (!response.ok) throw new ApiError(response.status, data);
      if ((!text && response.status !== 202) || (text && !json)) {
        throw new ClientError('invalid_response');
      }
      return data;
    } catch (error) {
      if (error instanceof ApiError || error instanceof ClientError)
        throw error;
      if (signal?.aborted) throw cancellationError(signal);
      throw new ClientError('network');
    }
  }

  function startCsrfRequest(): CsrfFlight {
    const controller = new AbortController();
    const promise = send('/api/v1/admin/auth/csrf', {
      method: 'GET',
      headers: { Accept: 'application/json' },
      signal: controller.signal,
    })
      .then((data) => {
        if (controller.signal.aborted)
          throw cancellationError(controller.signal);
        if (
          !isRecord(data) ||
          typeof data.token !== 'string' ||
          !data.token ||
          /\s/.test(data.token)
        ) {
          throw new ClientError('invalid_response');
        }
        const result: components['schemas']['AdminCsrfResponse'] = {
          token: data.token,
        };
        if (csrfFlight === flight) csrfToken = result.token;
        return result.token;
      })
      .finally(() => {
        if (csrfFlight === flight) csrfFlight = undefined;
      });
    const flight = { controller, promise, waiters: 0 };
    csrfFlight = flight;
    return flight;
  }

  async function getCsrfToken(signal?: AbortSignal): Promise<string> {
    if (signal?.aborted) throw cancellationError(signal);
    if (csrfToken) return csrfToken;
    const flight = csrfFlight ?? startCsrfRequest();
    flight.waiters += 1;
    try {
      return await waitWithSignal(flight.promise, signal);
    } finally {
      flight.waiters -= 1;
      if (flight.waiters === 0 && csrfFlight === flight) {
        csrfFlight = undefined;
        flight.controller.abort();
      }
    }
  }

  async function request<P extends ApiPath, M extends ApiMethod<P>>(
    method: M,
    path: P,
    options: RequestOptions<P, M>,
  ): Promise<ApiResponse<P, M>> {
    if (options.signal?.aborted) throw cancellationError(options.signal);
    const url = requestUrl(path, options.path, options.query);
    const admin = path.startsWith('/api/v1/admin/');
    const unsafe = !['get', 'head', 'options'].includes(method);
    const headers: Record<string, string> = { Accept: 'application/json' };
    if (options.body !== undefined)
      headers['Content-Type'] = 'application/json';
    try {
      if (admin && unsafe)
        headers['X-CSRF-TOKEN'] = await getCsrfToken(options.signal);
      if (options.signal?.aborted) throw cancellationError(options.signal);
      const data = await send(url, {
        method: method.toUpperCase(),
        headers,
        body:
          options.body === undefined ? undefined : JSON.stringify(options.body),
        signal: options.signal,
      });
      if (
        method === 'post' &&
        ['/api/v1/admin/auth/login', '/api/v1/admin/auth/logout'].includes(path)
      ) {
        clearCsrfToken();
      }
      // The only DTO assertion: the network boundary is described by generated OpenAPI types.
      return data as ApiResponse<P, M>;
    } catch (error) {
      if (
        admin &&
        error instanceof ApiError &&
        (error.status === 401 || error.code === 'csrf_invalid')
      ) {
        clearCsrfToken();
      }
      throw error;
    }
  }

  return {
    request,
    clearCsrfToken,
    async prepareCsrf(options: { signal?: AbortSignal } = {}): Promise<void> {
      await getCsrfToken(options.signal);
    },
  };
}

export const apiClient = createApiClient();
