import {
  AppstoreOutlined,
  AuditOutlined,
  TeamOutlined,
} from '@ant-design/icons';
import { Menu, type MenuProps } from 'antd';
import { Link, useLocation } from 'react-router-dom';

const items: MenuProps['items'] = [
  {
    key: 'console',
    icon: <AppstoreOutlined aria-hidden="true" />,
    label: <Link to="/admin">Dashboard</Link>,
  },
  {
    key: 'users',
    icon: <TeamOutlined aria-hidden="true" />,
    label: <Link to="/admin/users">Users</Link>,
  },
  {
    key: 'audit',
    icon: <AuditOutlined aria-hidden="true" />,
    label: <Link to="/admin/audit">Audit log</Link>,
  },
];

export function Navigation({ onNavigate }: { onNavigate?: () => void }) {
  const { pathname } = useLocation();
  return (
    <nav aria-label="Primary navigation">
      <Menu
        mode="inline"
        items={items}
        selectedKeys={
          pathname === '/admin'
            ? ['console']
            : /^\/admin\/users(?:\/|$)/.test(pathname)
              ? ['users']
              : pathname === '/admin/audit'
                ? ['audit']
                : []
        }
        onClick={onNavigate}
      />
    </nav>
  );
}
