import { useQuery } from '@tanstack/react-query';
import { queryKeys } from '@/api/queryKeys';
import { getUserSessions, sessionsQuery, type SessionState } from './api';

export function useUserSessions(
  userId: string,
  page: number,
  state: SessionState,
) {
  return useQuery({
    queryKey: queryKeys.users.sessions(userId, sessionsQuery(page, state)),
    queryFn: ({ signal }) => getUserSessions(userId, page, state, signal),
    staleTime: 30_000,
    networkMode: 'always',
  });
}
