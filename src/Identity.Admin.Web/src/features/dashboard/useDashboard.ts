import { useQuery } from '@tanstack/react-query';
import { queryKeys } from '@/api/queryKeys';
import { getDashboard } from './api';

export function useDashboard() {
  return useQuery({
    queryKey: queryKeys.dashboard(),
    queryFn: ({ signal }) => getDashboard(signal),
    staleTime: 30_000,
    networkMode: 'always',
  });
}
