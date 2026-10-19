import type { ApiResponse } from '@/api/contracts';

export const testUserDetails: ApiResponse<
  '/api/v1/admin/users/{userId}',
  'get'
> = {
  id: 'cd148904-c939-44af-90fe-e1c26b3f0f75',
  email: 'alice@example.test',
  displayName: null,
  status: 'Active',
  emailVerified: true,
  emailVerifiedAt: '2026-10-19T12:30:00Z',
  createdAt: '2026-10-19T12:00:00Z',
  updatedAt: '2026-10-19T12:30:00Z',
  disabledAt: null,
  disabledBy: null,
  disableReason: null,
  deactivatedAt: null,
  failedLoginCount: 0,
  lockedUntil: null,
};
export const testUserEvent = {
  id: 'ee148904-c939-44af-90fe-e1c26b3f0f75',
  userId: null,
  actorUserId: null,
  targetUserId: testUserDetails.id,
  eventType: 'verify_email',
  occurredAt: '2026-10-19T12:30:00Z',
  clientId: null,
  correlationId: 'test-correlation',
};
