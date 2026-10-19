import type { QueryParameters } from '@/api/contracts';

type UsersQuery = QueryParameters<'/api/v1/admin/users', 'get'>;
type SessionsQuery = QueryParameters<
  '/api/v1/admin/users/{userId}/sessions',
  'get'
>;
type AuditQuery = QueryParameters<'/api/v1/admin/audit-events', 'get'>;

export const queryKeys = {
  admin: ['admin'] as const,
  auth: { me: () => ['admin', 'auth', 'me'] as const },
  dashboard: () => ['admin', 'dashboard'] as const,
  users: {
    all: ['admin', 'users'] as const,
    list: (query: UsersQuery = {}) =>
      ['admin', 'users', 'list', { ...query }] as const,
    detail: (userId: string) => ['admin', 'users', 'detail', userId] as const,
    sessions: (userId: string, query: SessionsQuery = {}) =>
      ['admin', 'users', 'detail', userId, 'sessions', { ...query }] as const,
  },
  audit: {
    all: ['admin', 'audit'] as const,
    list: (query: AuditQuery = {}) =>
      ['admin', 'audit', 'list', { ...query }] as const,
  },
};
