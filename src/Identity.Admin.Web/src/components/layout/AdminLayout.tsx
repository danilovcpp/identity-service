import {
  MenuFoldOutlined,
  MenuOutlined,
  MenuUnfoldOutlined,
} from '@ant-design/icons';
import {
  Button,
  Drawer,
  Grid,
  Layout,
  Tooltip,
  Typography,
  type GetRef,
} from 'antd';
import { useRef, useState, type ReactNode } from 'react';
import { Outlet } from 'react-router-dom';
import { Brand } from '@/components/Brand';
import { Navigation } from '@/components/layout/Navigation';
import { containTabFocus } from '@/components/containTabFocus';

export function AdminLayout({
  headerContent,
  headerActions,
}: {
  headerContent?: ReactNode;
  headerActions?: ReactNode;
}) {
  const screens = Grid.useBreakpoint();
  const mobile = !screens.md;
  const [collapsed, setCollapsed] = useState(false);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const navigationTrigger = useRef<GetRef<typeof Button>>(null);
  return (
    <Layout className="admin-layout">
      <a href="#main-content" className="skip-link">
        Skip to content
      </a>
      {!mobile && (
        <Layout.Sider
          theme="light"
          width={240}
          collapsedWidth={80}
          collapsed={collapsed}
          breakpoint="lg"
          onBreakpoint={(broken) => {
            setCollapsed(broken);
            setDrawerOpen(false);
          }}
          className="admin-sidebar"
        >
          <div className="sidebar-brand">
            <Brand compact={collapsed} />
          </div>
          <div id="desktop-navigation">
            <Navigation />
          </div>
          {!collapsed && (
            <Typography.Text type="secondary" className="sidebar-caption">
              Standalone identity service
            </Typography.Text>
          )}
        </Layout.Sider>
      )}
      <Layout className="admin-workspace">
        <Layout.Header className="admin-topbar">
          <div className="topbar-context">
            {mobile ? (
              <Button
                type="text"
                icon={<MenuOutlined />}
                aria-label="Open navigation"
                ref={navigationTrigger}
                aria-expanded={drawerOpen}
                aria-controls="mobile-navigation"
                onClick={() => setDrawerOpen(true)}
              />
            ) : (
              <Tooltip
                title={collapsed ? 'Expand navigation' : 'Collapse navigation'}
              >
                <Button
                  type="text"
                  icon={
                    collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />
                  }
                  aria-label={
                    collapsed ? 'Expand navigation' : 'Collapse navigation'
                  }
                  aria-expanded={!collapsed}
                  aria-controls="desktop-navigation"
                  onClick={() => setCollapsed(!collapsed)}
                />
              </Tooltip>
            )}
            {headerContent ?? (
              <Typography.Text type="secondary">Administration</Typography.Text>
            )}
          </div>
          {headerActions && (
            <div className="topbar-actions">{headerActions}</div>
          )}
        </Layout.Header>
        <Layout.Content
          id="main-content"
          className="admin-content"
          role="main"
          tabIndex={-1}
        >
          <Outlet />
        </Layout.Content>
      </Layout>
      {mobile && (
        <Drawer
          title={<Brand onNavigate={() => setDrawerOpen(false)} />}
          placement="left"
          size={280}
          open={drawerOpen}
          onClose={() => setDrawerOpen(false)}
          focusable={{ trap: true, focusTriggerAfterClose: false }}
          afterOpenChange={(open) => {
            // Restore after the drawer's focus lock has released its DOM node.
            if (!open)
              requestAnimationFrame(() => navigationTrigger.current?.focus());
          }}
          onKeyDown={containTabFocus}
          styles={{ body: { padding: 0 } }}
        >
          <div id="mobile-navigation">
            <Navigation onNavigate={() => setDrawerOpen(false)} />
          </div>
        </Drawer>
      )}
    </Layout>
  );
}
