import { useQuery } from '@tanstack/react-query';
import { queryKeys } from '@/api/queryKeys';
import { getUserActivity, userActivityQuery } from './detailsApi';

export function useUserActivity(userId: string) {
  return useQuery({
    queryKey: queryKeys.audit.list(userActivityQuery(userId)),
    queryFn: ({ signal }) => getUserActivity(userId, signal),
    staleTime: 30_000,
    networkMode: 'always',
  });
}
