import js from '@eslint/js';
import { defineConfig, globalIgnores } from 'eslint/config';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default defineConfig([
  globalIgnores(['dist', 'coverage', 'playwright-report', 'test-results']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommendedTypeChecked,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
  },
  {
    files: ['src/**/*.{ts,tsx}'],
    languageOptions: { globals: globals.browser },
  },
  {
    files: ['src/**/*.{ts,tsx}'],
    ignores: [
      'src/**/*.test.{ts,tsx}',
      'src/test/**',
      'src/api/httpClient.ts',
      'src/api/schema.d.ts',
    ],
    rules: {
      'no-console': 'error',
      'no-restricted-globals': [
        'error',
        { name: 'fetch', message: 'Use the typed central API client.' },
        {
          name: 'localStorage',
          message: 'Do not persist console data or credentials.',
        },
        {
          name: 'sessionStorage',
          message: 'Do not persist console data or credentials.',
        },
      ],
      'no-restricted-properties': [
        'error',
        ...['window', 'globalThis'].flatMap((object) =>
          ['fetch', 'localStorage', 'sessionStorage'].map((property) => ({
            object,
            property,
            message: 'Use the central API client and in-memory state.',
          })),
        ),
      ],
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['@/test/**', '**/test/**'],
              message: 'Production code must not import test fixtures.',
            },
          ],
        },
      ],
    },
  },
  {
    files: ['*.js', 'scripts/*.mjs', 'vite.config.ts', 'playwright.config.ts'],
    extends: [js.configs.recommended],
    languageOptions: { globals: globals.node },
  },
  {
    files: ['e2e/*.ts'],
    languageOptions: { globals: { ...globals.browser, ...globals.node } },
  },
]);
