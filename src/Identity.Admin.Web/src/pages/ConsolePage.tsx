import { Card } from 'antd';
import { EmptyState } from '@/components/EmptyState';
import { PageHeader } from '@/components/PageHeader';

export function ConsolePage() {
  return (
    <>
      <PageHeader
        title="Identity Console"
        description="A dedicated workspace for Identity administration."
      />
      <Card>
        <EmptyState
          title="Administration workspace"
          description="User management and security activity will be available here."
        />
      </Card>
    </>
  );
}
