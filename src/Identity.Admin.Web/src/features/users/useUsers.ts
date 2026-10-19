import { useQuery } from '@tanstack/react-query';
import { queryKeys } from '@/api/queryKeys';
import { getUsers } from './api';
import { usersApiQuery, type UsersParameters } from './parameters';

export function useUsers(parameters: UsersParameters) {
  return useQuery({
    queryKey: queryKeys.users.list(usersApiQuery(parameters)),
    queryFn: ({ signal }) => getUsers(parameters, signal),
    staleTime: 30_000,
    networkMode: 'always',
  });
}
