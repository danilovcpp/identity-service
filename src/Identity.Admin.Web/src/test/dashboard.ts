import type { ApiResponse } from '@/api/contracts';

export const testDashboard = {
  totalUsers: 1234,
  activeUsers: 1000,
  activeSessions: 42,
  pendingVerificationUsers: 100,
  invitedUsers: 50,
  disabledUsers: 60,
  deactivatedUsers: 24,
  asOf: '2026-10-19T12:30:00Z',
  registrationTrendFrom: '2026-09-20T00:00:00Z',
  registrationTrendTo: '2026-10-20T00:00:00Z',
  registrationTrend: Array.from({ length: 30 }, (_, i) => ({
    date: new Date(Date.UTC(2026, 8, 20 + i)).toISOString().slice(0, 10),
    count: i === 29 ? 12 : i % 4,
  })),
} satisfies ApiResponse<'/api/v1/admin/dashboard', 'get'>;
