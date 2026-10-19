import { useQuery } from '@tanstack/react-query';
import { queryKeys } from '@/api/queryKeys';
import { getUserDetails } from './detailsApi';

export function useUserDetails(userId: string) {
  return useQuery({
    queryKey: queryKeys.users.detail(userId),
    queryFn: ({ signal }) => getUserDetails(userId, signal),
    staleTime: 30_000,
    networkMode: 'always',
  });
}
