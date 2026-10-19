import { Spin, Typography } from 'antd';

export function LoadingState({
  title = 'Loading content',
}: {
  title?: string;
}) {
  return (
    <section
      className="content-state"
      role="status"
      aria-live="polite"
      aria-label={title}
    >
      <Spin size="large" />
      <Typography.Text type="secondary">{title}</Typography.Text>
    </section>
  );
}
