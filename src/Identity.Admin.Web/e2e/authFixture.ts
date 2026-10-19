import { expect, type Page } from '@playwright/test';

export async function signIn(page: Page, readOnly = false) {
  const password = process.env.IDENTITY_E2E_PASSWORD;
  if (!password)
    throw new Error('Disposable fixture credentials are required.');
  await page.goto('/admin/login');
  await page
    .getByLabel('Email', { exact: true })
    .fill(readOnly ? 'reader@identity.example' : 'admin@identity.example');
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Identity Console', exact: true }),
  ).toBeVisible();
}
