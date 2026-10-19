import {
  Breadcrumb,
  Flex,
  Space,
  Typography,
  type BreadcrumbProps,
} from 'antd';
import type { ReactNode } from 'react';

type PageHeaderProps = {
  title: string;
  description?: string;
  breadcrumbs?: BreadcrumbProps['items'];
  actions?: ReactNode;
};

export function PageHeader({
  title,
  description,
  breadcrumbs,
  actions,
}: PageHeaderProps) {
  return (
    <header className="page-header">
      {breadcrumbs && <Breadcrumb items={breadcrumbs} />}
      <Flex align="start" justify="space-between" gap="middle" wrap>
        <div className="page-heading">
          <Typography.Title level={1}>{title}</Typography.Title>
          {description && (
            <Typography.Text type="secondary">{description}</Typography.Text>
          )}
        </div>
        {actions && <Space wrap>{actions}</Space>}
      </Flex>
    </header>
  );
}
