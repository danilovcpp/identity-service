import { Button, Result, Typography } from 'antd';
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
      <Result
        status="error"
        title="Unable to display content"
        subTitle={
          <>
            {message}
            {error instanceof ApiError && error.traceId && (
              <Typography.Paragraph type="secondary">
                Reference:{' '}
                <Typography.Text code>{error.traceId}</Typography.Text>
              </Typography.Paragraph>
            )}
          </>
        }
        extra={onRetry && <Button onClick={onRetry}>Try again</Button>}
      />
    </section>
  );
}
