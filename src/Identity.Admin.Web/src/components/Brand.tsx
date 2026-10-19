import { IdcardOutlined } from '@ant-design/icons';
import { Link } from 'react-router-dom';
import { Space, Typography } from 'antd';

export function Brand({
  compact = false,
  onNavigate,
}: {
  compact?: boolean;
  onNavigate?: () => void;
}) {
  return (
    <Link
      to="/admin"
      className="brand"
      aria-label="Identity Console home"
      onClick={onNavigate}
    >
      <Space>
        <span className="brand-mark" aria-hidden="true">
          <IdcardOutlined />
        </span>
        {!compact && <Typography.Text strong>Identity Console</Typography.Text>}
      </Space>
    </Link>
  );
}
