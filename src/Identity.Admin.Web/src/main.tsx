import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from '@/app/App';
import { AppProviders } from '@/app/AppProviders';
import 'antd/dist/reset.css';
import '@/app/styles.css';

const root = document.getElementById('root');

if (!root) {
  throw new Error('Application root element is missing.');
}

createRoot(root).render(
  <StrictMode>
    <AppProviders>
      <App />
    </AppProviders>
  </StrictMode>,
);
