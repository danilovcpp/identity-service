import { ExclamationCircleOutlined } from '@ant-design/icons';
import { Button, Typography } from 'antd';
import { ApiError, ClientError } from '@/api/errors';

export function ErrorState({
  error,
  onRetry,
}: {
  error?: unknown;
  onRetry?: () => void;
}) {
  const message =
    error instanceof ApiError || error instanceof ClientError
      ? error.message
      : 'We could not display this content. Please try again.';
  return (
    <section
      className="content-state"
      role="alert"
      aria-label="Unable to display content"
    >
      <ExclamationCircleOutlined
        className="state-error-icon"
        aria-hidden="true"
      />
      <div>
        <Typography.Title level={2}>Unable to display content</Typography.Title>
        <Typography.Paragraph type="secondary">{message}</Typography.Paragraph>
        {error instanceof ApiError && error.traceId && (
          <Typography.Paragraph type="secondary">
            Reference: <Typography.Text code>{error.traceId}</Typography.Text>
          </Typography.Paragraph>
        )}
      </div>
      {onRetry && <Button onClick={onRetry}>Try again</Button>}
    </section>
  );
}
