import { Button, Result, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';

export function NotFoundPage() {
  const navigate = useNavigate();
  return (
    <Result
      status="404"
      title={<Typography.Title level={1}>Page not found</Typography.Title>}
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
