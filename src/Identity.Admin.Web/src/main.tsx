import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { App } from '@/app/App';
import { AppProviders } from '@/app/AppProviders';
import 'antd/dist/reset.css';
import '@/app/styles.css';

const root = document.getElementById('root');

if (!root) {
  throw new Error('Application root element is missing.');
}

// AppErrorBoundary renders the fallback; never echo caught exception details to console.
createRoot(root, { onCaughtError: () => undefined }).render(
  <StrictMode>
    <AppProviders>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </AppProviders>
  </StrictMode>,
);
