import { readFileSync } from 'node:fs';
import { fileURLToPath, URL } from 'node:url';
import react from '@vitejs/plugin-react';
import { loadEnv } from 'vite';
import { defineConfig } from 'vitest/config';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'IDENTITY_');
  const certificateFile = env.IDENTITY_DEV_HTTPS_CERT_FILE;
  const keyFile = env.IDENTITY_DEV_HTTPS_KEY_FILE;

  if (Boolean(certificateFile) !== Boolean(keyFile)) {
    throw new Error(
      'HTTPS development requires both certificate and key files.',
    );
  }

  return {
    plugins: [react()],
    // No application configuration is embedded in the browser bundle at bootstrap.
    envPrefix: [],
    resolve: {
      alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
    },
    server: {
      host: '127.0.0.1',
      port: 5173,
      strictPort: true,
      https:
        certificateFile && keyFile
          ? {
              cert: readFileSync(certificateFile),
              key: readFileSync(keyFile),
            }
          : undefined,
      proxy: {
        '/api': {
          target: env.IDENTITY_API_PROXY_TARGET || 'http://localhost:5080',
          changeOrigin: true,
          secure: true,
        },
      },
    },
    preview: { host: '127.0.0.1', port: 4173, strictPort: true },
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
      include: ['src/**/*.test.{ts,tsx}'],
      clearMocks: true,
      restoreMocks: true,
    },
  };
});
