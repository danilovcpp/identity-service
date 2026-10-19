import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type PropsWithChildren,
} from 'react';
import { ApiError, ClientError } from '@/api/errors';
import { apiClient } from '@/api/httpClient';
import { queryKeys } from '@/api/queryKeys';
import { getSession, login, logout, type AdminProfile } from './api';

import { AuthContext, type Status } from './authContext';

export function AuthProvider({ children }: PropsWithChildren) {
  const client = useQueryClient();
  const [accessError, setAccessError] = useState<ApiError | null>(null);
  const [pending, setPending] = useState(false);
  const operation = useRef<AbortController | null>(null);
  const session = useQuery({
    queryKey: queryKeys.auth.me(),
    queryFn: ({ signal }) => getSession(signal),
    staleTime: 0,
    gcTime: 0,
    enabled: !pending,
  });

  const replaceSession = useCallback(
    async (current: AdminProfile | null) => {
      await client.cancelQueries();
      client.clear();
      apiClient.clearCsrfToken();
      client.setQueryData(queryKeys.auth.me(), current);
      setAccessError(null);
    },
    [client],
  );

  useEffect(
    () =>
      apiClient.onAccessError((error) => {
        if (error.status === 401) {
          // Hide the shell immediately, then cancel in-flight reads before dropping cache.
          setAccessError(error);
          void replaceSession(null);
        } else setAccessError(error);
      }),
    [replaceSession],
  );

  useEffect(() => {
    if (
      session.data === null ||
      accessError?.status === 403 ||
      (session.error instanceof ApiError && session.error.status === 403)
    ) {
      apiClient.clearCsrfToken();
      void client.cancelQueries({
        predicate: (query) => query.queryKey[1] !== 'auth',
      });
      client.removeQueries({
        predicate: (query) => query.queryKey[1] !== 'auth',
      });
      client.getMutationCache().clear();
    }
  }, [client, session.data, session.error, accessError]);

  useEffect(
    () => () => {
      operation.current?.abort();
    },
    [],
  );

  async function run(
    action: (signal: AbortSignal) => Promise<AdminProfile | null>,
  ) {
    if (operation.current) throw new ClientError('invalid_request');
    const controller = new AbortController();
    operation.current = controller;
    setPending(true);
    const timeout = setTimeout(
      () => controller.abort(new DOMException('Timeout', 'TimeoutError')),
      30_000,
    );
    try {
      // Prevent an old /me refetch overwriting a newly established identity.
      await client.cancelQueries();
      const current = await action(controller.signal);
      if (!controller.signal.aborted) await replaceSession(current);
    } finally {
      clearTimeout(timeout);
      operation.current = null;
      setPending(false);
    }
  }

  const error = accessError ?? session.error;
  const status: Status =
    error instanceof ApiError && error.status === 401
      ? 'unauthenticated'
      : error instanceof ApiError && error.status === 403
        ? 'forbidden'
        : error
          ? 'error'
          : session.isPending
            ? 'loading'
            : session.data
              ? 'authenticated'
              : 'unauthenticated';
  const current = status === 'authenticated' ? (session.data ?? null) : null;

  return (
    <AuthContext.Provider
      value={{
        status,
        profile: current,
        canWrite: current?.role === 'IdentityAdministrator',
        error,
        pending,
        signIn: (input) => run((signal) => login(input, signal)),
        signOut: () =>
          run(async (signal) => {
            try {
              await logout(signal);
            } catch (error) {
              if (!(error instanceof ApiError && error.status === 401))
                throw error;
            }
            return null;
          }),
        retry: () => {
          setAccessError(null);
          void session.refetch();
        },
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
