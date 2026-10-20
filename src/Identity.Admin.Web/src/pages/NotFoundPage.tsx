import { Button, Result } from 'antd';
import { PageTitle } from '@/components/PageTitle';
import { useNavigate } from 'react-router-dom';

export function NotFoundPage() {
  const navigate = useNavigate();
  return (
    <Result
      status="404"
      title={<PageTitle>Page not found</PageTitle>}
      subTitle="This page does not exist or is not available yet."
      extra={
        <Button
          type="primary"
          onClick={() => {
            void navigate('/admin');
          }}
        >
          Back to console
        </Button>
      }
    />
  );
}
