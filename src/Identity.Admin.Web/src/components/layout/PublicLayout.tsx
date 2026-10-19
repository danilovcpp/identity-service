import { Outlet } from 'react-router-dom';
import type { PropsWithChildren } from 'react';
import { Brand } from '@/components/Brand';

export function PublicLayout({ children }: PropsWithChildren) {
  return (
    <div className="public-layout">
      <header className="public-brand">
        <Brand />
      </header>
      <main className="public-content">{children ?? <Outlet />}</main>
    </div>
  );
}
