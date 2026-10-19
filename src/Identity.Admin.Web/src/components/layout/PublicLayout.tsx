import { Outlet } from 'react-router-dom';
import { Brand } from '@/components/Brand';

export function PublicLayout() {
  return (
    <div className="public-layout">
      <header className="public-brand">
        <Brand />
      </header>
      <main className="public-content">
        <Outlet />
      </main>
    </div>
  );
}
