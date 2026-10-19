import { Breadcrumb, Typography, type BreadcrumbProps } from 'antd';
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
      <div className="page-header-row">
        <div>
          <Typography.Title level={1}>{title}</Typography.Title>
          {description && (
            <Typography.Paragraph type="secondary">
              {description}
            </Typography.Paragraph>
          )}
        </div>
        {actions && <div className="page-header-actions">{actions}</div>}
      </div>
    </header>
  );
}
