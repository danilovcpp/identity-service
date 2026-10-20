import { test } from './fixture';
import { expect, type Page } from '@playwright/test';
import { signIn } from './authFixture';

test.describe.configure({ mode: 'serial' });
async function openAccount(page: Page, email = 'reader@identity.example') {
  await page.goto('/admin/users?search=' + encodeURIComponent(email));
  await page.getByRole('link', { name: `View details for ${email}` }).click();
  await expect(
    page.getByRole('region', { name: 'Account information' }),
  ).toBeVisible();
}
async function disable(page: Page) {
  await page
    .getByRole('button', { name: 'Disable account', exact: true })
    .click();
  const dialog = page.getByRole('dialog', { name: 'Disable account?' });
  await dialog
    .getByLabel('Reason', { exact: true })
    .fill('Disposable security review');
  const response = page.waitForResponse(
    (res) =>
      res.url().endsWith('/disable') && res.request().method() === 'POST',
  );
  await dialog.getByRole('button', { name: 'Confirm disable' }).click();
  return response;
}
async function enable(page: Page) {
  await page
    .getByRole('button', { name: 'Enable account', exact: true })
    .click();
  const dialog = page.getByRole('dialog', { name: 'Enable account?' });
  await expect(
    dialog.getByText(/Identity determines the restored status/),
  ).toBeVisible();
  const response = page.waitForResponse(
    (res) => res.url().endsWith('/enable') && res.request().method() === 'POST',
  );
  await dialog.getByRole('button', { name: 'Confirm enable' }).click();
  return response;
}

test('Disable blocks real sign-in and revokes the reader cookie; Enable refreshes server status, audit and catalog', async ({
  page,
  browser,
}) => {
  const password = process.env.IDENTITY_E2E_PASSWORD;
  if (!password) throw new Error('Disposable fixture password is required.');
  const readerContext = await browser.newContext({
    baseURL: process.env.IDENTITY_E2E_BASE_URL,
    ignoreHTTPSErrors: true,
  });
  try {
    const reader = await readerContext.newPage();
    await signIn(reader, true);
    await signIn(page);
    await openAccount(page);
    const nativeLogin = () =>
      page.request.post('/api/v1/auth/login', {
        data: {
          email: 'reader@identity.example',
          password,
          clientId: 'native',
        },
      });
    expect((await nativeLogin()).status()).toBe(200);
    expect((await disable(page)).status()).toBe(204);
    await expect(page.getByRole('dialog')).toBeHidden();
    const profile = page.getByRole('region', { name: 'Account information' });
    await expect(profile.getByText('Disabled', { exact: true })).toBeVisible();
    await expect(
      page.getByText('admin_disabled_user', { exact: true }),
    ).toBeVisible();
    expect((await nativeLogin()).status()).toBe(401);
    await reader.reload();
    await expect(
      reader.getByRole('heading', { name: 'Sign in to Identity' }),
    ).toBeVisible();
    await expect(
      page.getByRole('button', { name: 'Disable account', exact: true }),
    ).toHaveCount(0);
    expect((await enable(page)).status()).toBe(204);
    await expect(profile.getByText('Active', { exact: true })).toBeVisible();
    await expect(
      page.getByText('admin_enabled_user', { exact: true }),
    ).toBeVisible();
    expect((await nativeLogin()).status()).toBe(200);
    await page.getByRole('link', { name: 'Back to users' }).click();
    await expect(
      page.getByRole('cell', { name: 'Active', exact: true }),
    ).toBeVisible();
    expect(
      await page.evaluate(() => ({
        local: localStorage.length,
        session: sessionStorage.length,
      })),
    ).toEqual({ local: 0, session: 0 });
  } finally {
    await readerContext.close();
  }
});

test('Enable restores Invited from the backend instead of assuming Active', async ({
  page,
}) => {
  await signIn(page);
  await page.goto('/admin/users?search=zz-invited-');
  await page
    .getByRole('link', { name: /^View details for zz-invited-/ })
    .click();
  const profile = page.getByRole('region', { name: 'Account information' });
  await expect(profile.getByText('Invited', { exact: true })).toBeVisible();
  expect((await disable(page)).status()).toBe(204);
  await expect(profile.getByText('Disabled', { exact: true })).toBeVisible();
  expect((await enable(page)).status()).toBe(204);
  await expect(profile.getByText('Invited', { exact: true })).toBeVisible();
  await expect(profile.getByText('Not verified', { exact: true })).toHaveCount(
    2,
  );
  await expect(
    page.getByRole('button', { name: 'Resend invitation', exact: true }),
  ).toBeVisible();
});

test('the real last-administrator guard returns 409 without changing status or ending the session', async ({
  page,
}) => {
  await signIn(page);
  await openAccount(page, 'admin@identity.example');
  let posts = 0;
  page.on('request', (request) => {
    if (request.url().endsWith('/disable') && request.method() === 'POST')
      posts++;
  });
  expect((await disable(page)).status()).toBe(409);
  const dialog = page.getByRole('dialog', { name: 'Disable account?' });
  await expect(dialog.getByRole('alert')).toContainText(
    'The last active administrator cannot be disabled',
  );
  await expect(
    page
      .getByRole('region', { name: 'Account information' })
      .getByText('Active', { exact: true }),
  ).toBeVisible();
  expect(posts).toBe(1);
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(
    page.getByRole('button', { name: 'Disable account', exact: true }),
  ).toBeFocused();
});

test('ReadOnly has no status controls and a forbidden command clears the administrator view', async ({
  page,
}) => {
  await signIn(page, true);
  await openAccount(page, 'admin@identity.example');
  await expect(
    page.getByRole('button', { name: /Disable account|Enable account/ }),
  ).toHaveCount(0);
  await page.getByRole('button', { name: 'Sign out', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Sign in to Identity' }),
  ).toBeVisible();
  // Choose Dashboard as the return path. Reloading the same login URL retains
  // history.state and would correctly return to the former account instead.
  await page.goto('/admin');
  await expect(
    page.getByRole('heading', { name: 'Sign in to Identity' }),
  ).toBeVisible();
  await signIn(page);
  await openAccount(page);
  await page.route('**/disable', (route) =>
    route.fulfill({
      status: 403,
      contentType: 'application/problem+json',
      body: JSON.stringify({ code: 'forbidden' }),
    }),
  );
  expect((await disable(page)).status()).toBe(403);
  await expect(
    page.getByRole('heading', { name: 'Access denied', exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole('region', { name: 'Account information' }),
  ).toHaveCount(0);
});

test('network failure leaves status unchanged and never repeats the command', async ({
  page,
}) => {
  await signIn(page);
  await openAccount(page);
  let posts = 0;
  await page.route('**/disable', (route) => {
    posts++;
    return route.abort('failed');
  });
  await page
    .getByRole('button', { name: 'Disable account', exact: true })
    .click();
  const dialog = page.getByRole('dialog', { name: 'Disable account?' });
  await dialog
    .getByLabel('Reason', { exact: true })
    .fill('Disposable security review');
  await dialog.getByRole('button', { name: 'Confirm disable' }).click();
  await expect(dialog.getByRole('alert')).toContainText(
    'The result could not be confirmed',
  );
  await expect(
    page
      .getByRole('region', { name: 'Account information' })
      .getByText('Active', { exact: true }),
  ).toBeVisible();
  expect(posts).toBe(1);
});

for (const width of [360, 768, 1280]) {
  test(`Disable confirmation validates and traps keyboard focus at ${width}px`, async ({
    page,
  }, testInfo) => {
    await page.setViewportSize({ width, height: 900 });
    await signIn(page);
    await openAccount(page);
    const trigger = page.getByRole('button', {
      name: 'Disable account',
      exact: true,
    });
    await trigger.focus();
    await page.keyboard.press('Enter');
    const dialog = page.getByRole('dialog', { name: 'Disable account?' });
    const cancel = dialog.getByRole('button', { name: 'Cancel', exact: true });
    await expect(cancel).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(
      dialog.getByRole('button', { name: 'Confirm disable' }),
    ).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(
      dialog.getByText('Enter a reason for disabling this account.'),
    ).toBeVisible();
    await page.keyboard.press('Tab');
    await expect(dialog.getByRole('button', { name: 'Close' })).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(dialog.getByLabel('Reason', { exact: true })).toBeFocused();
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true);
    await page.screenshot({
      path: testInfo.outputPath('disable-account.png'),
      fullPage: true,
    });
    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
    await expect(trigger).toBeFocused();
  });
}
