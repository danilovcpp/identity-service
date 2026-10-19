import { IdcardOutlined } from '@ant-design/icons';
import { Link } from 'react-router-dom';

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
      <span className="brand-mark" aria-hidden="true">
        <IdcardOutlined />
      </span>
      {!compact && (
        <span className="brand-name">
          Identity <span>Console</span>
        </span>
      )}
    </Link>
  );
}
