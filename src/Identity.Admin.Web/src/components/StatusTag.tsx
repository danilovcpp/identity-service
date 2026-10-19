import { Tag, type TagProps } from 'antd';
import type { components } from '@/api/schema';

// Exact UserStatus values emitted by the backend; labels are not new states.
const labels = new Map<string, { label: string; color: TagProps['color'] }>([
  ['PendingVerification', { label: 'Pending verification', color: 'warning' }],
  ['Active', { label: 'Active', color: 'success' }],
  ['Disabled', { label: 'Disabled', color: 'error' }],
  ['Deactivated', { label: 'Deactivated', color: 'default' }],
  ['Invited', { label: 'Invited', color: 'processing' }],
]);

export function StatusTag({
  status,
}: {
  status: components['schemas']['AdminUserListItem']['status'];
}) {
  const display = labels.get(status);
  return (
    <Tag color={display?.color}>
      {display?.label ?? (status || 'Not provided')}
    </Tag>
  );
}
