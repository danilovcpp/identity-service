import { test } from './fixture';
import { expect, type Page } from '@playwright/test';
import { signIn } from './authFixture';

test.describe.configure({ mode: 'serial' });
const email = 'reader@identity.example';
const triggerName = 'Send password reset link';
const success = 'Password reset request accepted for processing.';
const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null;

async function openAccount(page: Page) {
  await page.goto('/admin/users?search=' + encodeURIComponent(email));
  await page.getByRole('link', { name: `View details for ${email}` }).click();
  await expect(
    page.getByRole('region', { name: 'Account information' }),
  ).toBeVisible();
}
async function openConfirmation(page: Page) {
  await page.getByRole('button', { name: triggerName, exact: true }).click();
  const dialog = page.getByRole('dialog', { name: triggerName + '?' });
  await expect(dialog).toBeVisible();
  return dialog;
}
async function deliveredResets(page: Page) {
  const mailpit = process.env.IDENTITY_E2E_MAILPIT_URL;
  if (!mailpit) throw new Error('Disposable Mailpit fixture is required.');
  const response = await page.request.get(`${mailpit}/api/v1/messages`);
  expect(response.status()).toBe(200);
  const data: unknown = await response.json();
  if (!isRecord(data) || !Array.isArray(data.messages))
    throw new Error('Invalid Mailpit response.');
  // Envelope only. Never load a message body containing a reset link or token.
  return data.messages.filter(
    (item: unknown) =>
      isRecord(item) &&
      Array.isArray(item.To) &&
      item.To.some(
        (recipient: unknown) =>
          isRecord(recipient) && recipient.Address === email,
      ) &&
      typeof item.Subject === 'string' &&
      /reset/i.test(item.Subject),
  ).length;
}

test('confirmed reset returns real 202, refreshes audit and delivers email without returning a password or token', async ({
  page,
}) => {
  await signIn(page);
  await openAccount(page);
  const before = await deliveredResets(page);
  let posts = 0;
  page.on('request', (request) => {
    if (
      request.url().endsWith('/password-reset') &&
      request.method() === 'POST'
    )
      posts++;
  });
  let dialog = await openConfirmation(page);
  await expect(dialog.getByText(email, { exact: true })).toBeVisible();
  await expect(dialog).toContainText('administrators cannot see or set it');
  await expect(dialog.locator('input')).toHaveCount(0);
  expect(posts).toBe(0);
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(
    page.getByRole('button', { name: triggerName, exact: true }),
  ).toBeFocused();
  expect(posts).toBe(0);
  dialog = await openConfirmation(page);
  const accepted = page.waitForResponse(
    (res) =>
      res.url().endsWith('/password-reset') &&
      res.request().method() === 'POST',
  );
  await dialog.getByRole('button', { name: 'Request reset email' }).click();
  const response = await accepted;
  expect(response.status()).toBe(202);
  expect(response.request().postData()).toBeNull();
  expect(Boolean(response.request().headers()['x-csrf-token'])).toBe(true);
  expect(await response.body()).toHaveLength(0);
  await expect(dialog).toBeHidden();
  await expect(page.getByText(success, { exact: true })).toBeVisible();
  await expect(
    page.getByText('admin_requested_password_reset', { exact: true }),
  ).toBeVisible();
  await expect.poll(() => deliveredResets(page)).toBe(before + 1);
  expect(posts).toBe(1);
  await expect(page.locator('input[type=password]')).toHaveCount(0);
  expect(
    await page.evaluate(() => ({
      local: localStorage.length,
      session: sessionStorage.length,
    })),
  ).toEqual({ local: 0, session: 0 });
});

test('ReadOnly has no reset action and the real backend rejects a direct command', async ({
  page,
}) => {
  await signIn(page, true);
  await openAccount(page);
  await expect(
    page.getByRole('button', { name: triggerName, exact: true }),
  ).toHaveCount(0);
  const csrf: unknown = await (
    await page.request.get('/api/v1/admin/auth/csrf')
  ).json();
  if (!isRecord(csrf) || typeof csrf.token !== 'string')
    throw new Error('Invalid CSRF fixture.');
  const userId = new URL(page.url()).pathname.split('/').at(-1);
  expect(
    (
      await page.request.post(`/api/v1/admin/users/${userId}/password-reset`, {
        headers: { 'X-CSRF-TOKEN': csrf.token },
      })
    ).status(),
  ).toBe(403);
});

test('a concurrent account disable produces real 409 without claiming acceptance', async ({
  page,
}) => {
  await signIn(page);
  await openAccount(page);
  const dialog = await openConfirmation(page);
  const csrf: unknown = await (
    await page.request.get('/api/v1/admin/auth/csrf')
  ).json();
  if (!isRecord(csrf) || typeof csrf.token !== 'string')
    throw new Error('Invalid CSRF fixture.');
  const path =
    '/api/v1/admin/users/' + new URL(page.url()).pathname.split('/').at(-1);
  const headers = { 'X-CSRF-TOKEN': csrf.token };
  expect(
    (
      await page.request.post(path + '/disable', {
        headers,
        data: { reason: 'Disposable concurrent reset check' },
      })
    ).status(),
  ).toBe(204);
  try {
    const rejected = page.waitForResponse(
      (res) =>
        res.url().endsWith('/password-reset') &&
        res.request().method() === 'POST',
    );
    await dialog.getByRole('button', { name: 'Request reset email' }).click();
    expect((await rejected).status()).toBe(409);
    await expect(dialog.getByRole('alert')).toContainText(
      'This account is no longer eligible',
    );
    await expect(page.getByText(success, { exact: true })).toHaveCount(0);
  } finally {
    expect(
      (await page.request.post(path + '/enable', { headers })).status(),
    ).toBe(204);
  }
});

test('network failure stays uncertain and never automatically repeats a reset request', async ({
  page,
}) => {
  await signIn(page);
  await openAccount(page);
  let posts = 0;
  await page.route('**/password-reset', (route) => {
    posts++;
    return route.abort('failed');
  });
  const dialog = await openConfirmation(page);
  await dialog.getByRole('button', { name: 'Request reset email' }).click();
  await expect(dialog.getByRole('alert')).toContainText(
    'The result could not be confirmed',
  );
  await expect(page.getByText(success, { exact: true })).toHaveCount(0);
  expect(posts).toBe(1);
});

test('a forbidden reset response clears the protected account view', async ({
  page,
}) => {
  await signIn(page);
  await openAccount(page);
  await page.route('**/password-reset', (route) =>
    route.fulfill({
      status: 403,
      contentType: 'application/problem+json',
      body: JSON.stringify({ code: 'forbidden' }),
    }),
  );
  const dialog = await openConfirmation(page);
  await dialog.getByRole('button', { name: 'Request reset email' }).click();
  await expect(
    page.getByRole('heading', { name: 'Access denied', exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole('region', { name: 'Account information' }),
  ).toHaveCount(0);
});

for (const width of [360, 768, 1280]) {
  test(`reset confirmation supports keyboard focus and responsive layout at ${width}px`, async ({
    page,
  }, testInfo) => {
    await page.setViewportSize({ width, height: 900 });
    await signIn(page);
    await openAccount(page);
    const trigger = page.getByRole('button', {
      name: triggerName,
      exact: true,
    });
    await trigger.focus();
    await page.keyboard.press('Enter');
    const dialog = page.getByRole('dialog', { name: triggerName + '?' });
    await expect(dialog).toBeVisible();
    await expect(dialog).not.toHaveClass(
      /ant-zoom-(?:enter|appear)(?:-active|-prepare)?(?:\s|$)/,
    );
    await expect(dialog).toHaveCSS('opacity', '1');
    await expect(
      dialog.getByRole('button', { name: 'Cancel', exact: true }),
    ).toBeFocused();
    for (let i = 0; i < 5; i++) {
      await page.keyboard.press('Tab');
      expect(
        await dialog.evaluate((element) =>
          element.contains(document.activeElement),
        ),
      ).toBe(true);
    }
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true);
    await page.screenshot({
      path: testInfo.outputPath('password-reset.png'),
      fullPage: true,
      animations: 'disabled',
    });
    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
    await expect(trigger).toBeFocused();
  });
}
