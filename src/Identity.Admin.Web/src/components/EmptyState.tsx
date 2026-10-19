import { Empty, Typography } from 'antd';
import type { ReactNode } from 'react';

type EmptyStateProps = {
  title?: string;
  description?: string;
  action?: ReactNode;
};

export function EmptyState({
  title = 'Nothing to show yet',
  description,
  action,
}: EmptyStateProps) {
  return (
    <section className="content-state" aria-label={title}>
      <Empty
        image={Empty.PRESENTED_IMAGE_SIMPLE}
        description={
          <>
            <Typography.Title level={2}>{title}</Typography.Title>
            {description && (
              <Typography.Paragraph type="secondary">
                {description}
              </Typography.Paragraph>
            )}
          </>
        }
      >
        {action}
      </Empty>
    </section>
  );
}
