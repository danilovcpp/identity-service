import { Card, Descriptions, Typography } from 'antd';
import { StatusTag } from '@/components/StatusTag';
import { LocalDateTime } from '@/components/LocalDateTime';
import { CopyUserId } from './CopyUserId';
import type { UserDetails } from './detailsApi';

export function UserProfile({ data }: { data: UserDetails }) {
  const date = (value: string | null, empty: string) =>
    value ? <LocalDateTime value={value} /> : empty;
  return (
    <section aria-label="Account information" className="user-profile">
      <Card
        title={
          <Typography.Title level={2}>Account information</Typography.Title>
        }
      >
        <Descriptions
          layout="vertical"
          column={{ xs: 1, sm: 2, lg: 3 }}
          items={[
            { key: 'email', label: 'Email', children: data.email },
            {
              key: 'name',
              label: 'Display name',
              children: data.displayName || 'Not provided',
            },
            {
              key: 'status',
              label: 'Status',
              children: <StatusTag status={data.status} />,
            },
            {
              key: 'id',
              label: 'User ID',
              span: 'filled',
              children: <CopyUserId value={data.id} />,
            },
            {
              key: 'verified',
              label: 'Email verification',
              children: data.emailVerified ? 'Verified' : 'Not verified',
            },
            {
              key: 'verifiedAt',
              label: 'Email verified at',
              children: date(data.emailVerifiedAt, 'Not verified'),
            },
            {
              key: 'created',
              label: 'Created',
              children: <LocalDateTime value={data.createdAt} />,
            },
            {
              key: 'updated',
              label: 'Last updated',
              children: <LocalDateTime value={data.updatedAt} />,
            },
            {
              key: 'disabled',
              label: 'Disabled at',
              children: date(data.disabledAt, 'Not disabled'),
            },
            {
              key: 'deactivated',
              label: 'Deactivated at',
              children: date(data.deactivatedAt, 'Not deactivated'),
            },
          ]}
        />
        <Typography.Paragraph type="secondary">
          Dates are shown in your local time. Hover over a date for UTC.
        </Typography.Paragraph>
      </Card>
    </section>
  );
}
