import { Outlet } from 'react-router-dom';
import type { PropsWithChildren } from 'react';
import { Brand } from '@/components/Brand';

export function PublicLayout({ children }: PropsWithChildren) {
  return (
    <div className="public-layout">
      <a href="#main-content" className="skip-link">
        Skip to content
      </a>
      <header className="public-brand">
        <Brand />
      </header>
      <main id="main-content" tabIndex={-1} className="public-content">
        {children ?? <Outlet />}
      </main>
    </div>
  );
}
