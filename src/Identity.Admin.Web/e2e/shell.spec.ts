import { expect, test } from '@playwright/test';
import { signIn } from './authFixture';

test.beforeEach(async ({ page }) => {
  await signIn(page);
});

for (const width of [360, 768, 1280]) {
  test(`shell at ${width}px has usable navigation and only dashboard reads`, async ({
    page,
  }, info) => {
    const errors: string[] = [];
    const apiRequests: string[] = [];
    page.on('pageerror', (error) => errors.push(error.name));
    page.on('request', (request) => {
      if (
        new URL(request.url()).pathname.startsWith('/api/') &&
        !new URL(request.url()).pathname.startsWith('/api/v1/admin/auth/') &&
        new URL(request.url()).pathname !== '/api/v1/admin/dashboard'
      )
        apiRequests.push(request.method());
    });
    await page.setViewportSize({ width, height: 900 });
    await page.goto('/admin');
    await expect(
      page.getByRole('heading', { level: 1, name: 'Dashboard' }),
    ).toBeVisible();
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
    if (width < 768) {
      const open = page.getByRole('button', { name: 'Open navigation' });
      await open.click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();
      await expect(
        dialog.getByRole('link', { name: 'Users', exact: true }),
      ).toBeVisible();
      await dialog
        .getByRole('link', { name: 'Dashboard', exact: true })
        .click();
      await expect(dialog).not.toBeVisible();
    } else {
      await expect(
        page.getByRole('navigation', { name: 'Primary navigation' }),
      ).toBeVisible();
      const toggle = page.getByRole('button', {
        name: width === 768 ? 'Expand navigation' : 'Collapse navigation',
      });
      await expect(toggle).toBeVisible();
      await toggle.click();
      await expect(
        page.getByRole('button', {
          name: width === 768 ? 'Collapse navigation' : 'Expand navigation',
        }),
      ).toBeVisible();
      await page
        .getByRole('button', {
          name: width === 768 ? 'Collapse navigation' : 'Expand navigation',
        })
        .click();
    }
    await expect(
      page.getByRole('link', { name: 'Users', exact: true }),
    ).toHaveCount(width < 768 ? 0 : 1);
    expect(
      await page.evaluate(() => localStorage.length + sessionStorage.length),
    ).toBe(0);
    expect(apiRequests).toEqual([]);
    expect(errors).toEqual([]);
    await expect(page.getByRole('application')).toBeVisible();
    // Wait for ResizeObserver after sidebar transitions, not just the initial heading.
    await expect
      .poll(() => page.evaluate(() => document.documentElement.scrollWidth))
      .toBe(width);
    await page.screenshot({
      path: info.outputPath('shell.png'),
      fullPage: true,
    });
  });
}

test('keyboard skip link, drawer trap, Escape and focus restoration', async ({
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 900 });
  await page.goto('/admin');
  await expect(
    page.getByRole('heading', { name: 'Dashboard', exact: true }),
  ).toBeVisible();
  await page.keyboard.press('Tab');
  await expect(
    page.getByRole('link', { name: 'Skip to content' }),
  ).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('main')).toBeFocused();
  await page.keyboard.press('Shift+Tab');
  await page.keyboard.press('Shift+Tab');
  const open = page.getByRole('button', { name: 'Open navigation' });
  await expect(open).toBeFocused();
  await page.keyboard.press('Enter');
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  for (let i = 0; i < 8; i++) {
    await page.keyboard.press('Tab');
    expect(
      await dialog.evaluate((element) =>
        element.contains(document.activeElement),
      ),
    ).toBe(true);
  }
  await page.keyboard.press('Escape');
  await expect(dialog).not.toBeVisible();
  await expect(open).toBeFocused();
});

test('admin/public unknown paths return safely to the console without a document reload', async ({
  page,
}) => {
  for (const route of [
    '/admin/users/missing/unknown',
    '/account/activate',
    '/missing',
  ]) {
    await page.goto(route);
    await expect(
      page.getByRole('heading', { level: 1, name: 'Page not found' }),
    ).toBeVisible();
    const navigations: string[] = [];
    page.on('request', (request) => {
      if (request.isNavigationRequest()) navigations.push(request.method());
    });
    await page.getByRole('button', { name: 'Back to console' }).click();
    await expect(page).toHaveURL(/\/admin$/);
    await expect(
      page.getByRole('heading', { name: 'Dashboard', level: 1 }),
    ).toBeVisible();
    expect(navigations).toEqual([]);
    page.removeAllListeners('request');
  }
});

test('resizing an open drawer to desktop and back does not reopen it', async ({
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 900 });
  await page.goto('/admin');
  await page.getByRole('button', { name: 'Open navigation' }).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await page.setViewportSize({ width: 1280, height: 900 });
  await expect(
    page.getByRole('button', { name: 'Collapse navigation' }),
  ).toBeVisible();
  await expect(page.getByRole('dialog')).not.toBeVisible();
  await page.setViewportSize({ width: 360, height: 900 });
  await expect(
    page.getByRole('button', { name: 'Open navigation' }),
  ).toHaveAttribute('aria-expanded', 'false');
  await expect(page.getByRole('dialog')).not.toBeVisible();
});
