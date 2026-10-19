import { useQuery } from '@tanstack/react-query';
import { queryKeys } from '@/api/queryKeys';
import { getAuditEvents } from './api';
import { auditApiQuery, type AuditParameters } from './parameters';

export function useAuditEvents(values: AuditParameters) {
  return useQuery({
    queryKey: queryKeys.audit.list(auditApiQuery(values)),
    queryFn: ({ signal }) => getAuditEvents(values, signal),
    staleTime: 30_000,
    networkMode: 'always',
  });
}
