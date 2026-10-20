import { test } from './fixture';
import { expect, type Page } from '@playwright/test';
import { signIn } from './authFixture';

test.describe.configure({ mode: 'serial' });
const email = 'reader@identity.example';
const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null;
const section = (page: Page) =>
  page.getByRole('region', { name: 'User sessions', exact: true });
async function openAccount(page: Page) {
  await page.goto('/admin/users?search=' + encodeURIComponent(email));
  await page.getByRole('link', { name: `View details for ${email}` }).click();
  await expect(
    section(page).getByRole('heading', { name: 'Sessions', exact: true }),
  ).toBeVisible();
}
async function nativeSession(page: Page) {
  const password = process.env.IDENTITY_E2E_PASSWORD;
  if (!password) throw new Error('Disposable fixture password is required.');
  const login = await page.request.post('/api/v1/auth/login', {
    data: { email, password, clientId: 'native' },
  });
  expect(login.status()).toBe(200);
  const tokens: unknown = await login.json();
  if (
    !isRecord(tokens) ||
    typeof tokens.accessToken !== 'string' ||
    typeof tokens.refreshToken !== 'string'
  )
    throw new Error('Invalid native fixture response.');
  const response = await page.request.get('/api/v1/sessions', {
    headers: { Authorization: `Bearer ${tokens.accessToken}` },
  });
  expect(response.status()).toBe(200);
  const sessions: unknown = await response.json();
  if (!Array.isArray(sessions))
    throw new Error('Invalid session fixture response.');
  const current: unknown = sessions.find(
    (value: unknown) => isRecord(value) && value.isCurrent === true,
  );
  if (!isRecord(current) || typeof current.id !== 'string')
    throw new Error('Missing native fixture session.');
  // Secrets stay in the Node test's closure, never browser JS/storage or test artifacts.
  return {
    id: current.id,
    accessToken: tokens.accessToken,
    refreshToken: tokens.refreshToken,
  };
}
async function openSingle(page: Page, id: string) {
  await section(page)
    .getByRole('button', { name: `Revoke session ${id}`, exact: true })
    .click();
  const dialog = page.getByRole('dialog', {
    name: 'Revoke session?',
    exact: true,
  });
  await expect(dialog).toBeVisible();
  return dialog;
}

test('single revoke stops only the chosen refresh session; revoke all stops remaining refreshes and the reader cookie', async ({
  page,
  browser,
}) => {
  await signIn(page);
  const first = await nativeSession(page);
  const second = await nativeSession(page);
  const readerContext = await browser.newContext({
    baseURL: process.env.IDENTITY_E2E_BASE_URL,
    ignoreHTTPSErrors: true,
  });
  try {
    const reader = await readerContext.newPage();
    await signIn(reader, true);
    await openAccount(page);
    let unsafe = 0;
    page.on('request', (request) => {
      if (/\/sessions\//.test(request.url()) && request.method() !== 'GET')
        unsafe++;
    });
    let dialog = await openSingle(page, first.id);
    await expect(dialog.getByText(first.id, { exact: true })).toBeVisible();
    expect(unsafe).toBe(0);
    await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
    await expect(dialog).toBeHidden();
    expect(unsafe).toBe(0);
    dialog = await openSingle(page, first.id);
    const revoked = page.waitForResponse(
      (res) =>
        res.url().endsWith('/sessions/' + first.id) &&
        res.request().method() === 'DELETE',
    );
    await dialog
      .getByRole('button', { name: 'Confirm revoke session' })
      .click();
    expect((await revoked).status()).toBe(204);
    await expect(dialog).toBeHidden();
    const row = section(page).getByRole('row').filter({ hasText: first.id });
    await expect(row.getByText('Revoked', { exact: true })).toBeVisible();
    await expect(row.getByRole('button')).toHaveCount(0);
    await expect(
      section(page).getByRole('button', {
        name: 'Refresh sessions',
        exact: true,
      }),
    ).toBeFocused();
    await expect(
      page.getByText('admin_revoked_session', { exact: true }),
    ).toBeVisible();
    expect(
      (
        await page.request.post('/api/v1/auth/refresh', {
          data: { refreshToken: first.refreshToken },
        })
      ).status(),
    ).toBe(401);
    expect(
      (
        await page.request.get('/api/v1/sessions', {
          headers: { Authorization: `Bearer ${second.accessToken}` },
        })
      ).status(),
    ).toBe(200);
    expect(unsafe).toBe(1);
    await section(page)
      .getByRole('button', { name: 'Revoke all sessions', exact: true })
      .click();
    const allDialog = page.getByRole('dialog', {
      name: 'Revoke all sessions?',
      exact: true,
    });
    await expect(allDialog).toContainText(
      'all refresh and administrator browser sessions',
    );
    const all = page.waitForResponse(
      (res) =>
        res.url().endsWith('/sessions/revoke-all') &&
        res.request().method() === 'POST',
    );
    await allDialog.getByRole('button', { name: 'Confirm revoke all' }).click();
    expect((await all).status()).toBe(204);
    await expect(
      page.getByText('All sessions revoked.', { exact: true }),
    ).toBeVisible();
    await expect(
      page.getByText('admin_revoked_all_sessions', { exact: true }),
    ).toBeVisible();
    expect(
      (
        await page.request.post('/api/v1/auth/refresh', {
          data: { refreshToken: second.refreshToken },
        })
      ).status(),
    ).toBe(401);
    await reader.reload();
    await expect(
      reader.getByRole('heading', { name: 'Sign in to Identity' }),
    ).toBeVisible();
    expect(unsafe).toBe(2);
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

test('ReadOnly reads sessions but both UI actions and direct backend writes are forbidden', async ({
  page,
}) => {
  await signIn(page, true);
  const native = await nativeSession(page);
  await openAccount(page);
  await expect(
    section(page).getByText(native.id, { exact: true }),
  ).toBeVisible();
  await expect(
    section(page).getByRole('button', { name: /Revoke/ }),
  ).toHaveCount(0);
  const csrf: unknown = await (
    await page.request.get('/api/v1/admin/auth/csrf')
  ).json();
  if (!isRecord(csrf) || typeof csrf.token !== 'string')
    throw new Error('Invalid CSRF fixture.');
  const path =
    '/api/v1/admin/users/' +
    new URL(page.url()).pathname.split('/').at(-1) +
    '/sessions/';
  const headers = { 'X-CSRF-TOKEN': csrf.token };
  expect(
    (await page.request.delete(path + native.id, { headers })).status(),
  ).toBe(403);
  expect(
    (await page.request.post(path + 'revoke-all', { headers })).status(),
  ).toBe(403);
});

test('state filter uses the real server and supports empty results', async ({
  page,
}) => {
  await signIn(page);
  await openAccount(page);
  await section(page).getByRole('combobox', { name: 'Session state' }).click();
  const filtered = page.waitForResponse((res) =>
    res.url().includes('/sessions?page=1&pageSize=25&state=expired'),
  );
  await page.getByRole('option', { name: 'Expired', exact: true }).click();
  expect((await filtered).status()).toBe(200);
  await expect(
    section(page).getByRole('heading', { name: 'No sessions found' }),
  ).toBeVisible();
});

for (const status of [403, 404, 409]) {
  test(`revoke handles ${status} without success or automatic replay`, async ({
    page,
  }) => {
    await signIn(page);
    const native = await nativeSession(page);
    await openAccount(page);
    let requests = 0;
    await page.route('**/sessions/' + native.id, (route) => {
      requests++;
      return route.fulfill({
        status,
        contentType: 'application/problem+json',
        body: JSON.stringify({
          code: status === 403 ? 'forbidden' : 'session_not_found',
        }),
      });
    });
    const dialog = await openSingle(page, native.id);
    await dialog
      .getByRole('button', { name: 'Confirm revoke session' })
      .click();
    if (status === 403) {
      await expect(
        page.getByRole('heading', { name: 'Access denied', exact: true }),
      ).toBeVisible();
      await expect(section(page)).toHaveCount(0);
    } else {
      await expect(dialog.getByRole('alert')).toContainText(
        status === 404 ? 'no longer available' : 'state has changed',
      );
      await expect(
        section(page)
          .getByRole('row')
          .filter({ hasText: native.id })
          .getByText('Active', { exact: true }),
      ).toBeVisible();
    }
    await expect(
      page.getByText('Session revoked.', { exact: true }),
    ).toHaveCount(0);
    expect(requests).toBe(1);
  });
}

test('network failure of revoke-all is uncertain and does not retry', async ({
  page,
}) => {
  await signIn(page);
  await openAccount(page);
  let requests = 0;
  await page.route('**/sessions/revoke-all', (route) => {
    requests++;
    return route.abort('failed');
  });
  await section(page)
    .getByRole('button', { name: 'Revoke all sessions', exact: true })
    .click();
  const dialog = page.getByRole('dialog', {
    name: 'Revoke all sessions?',
    exact: true,
  });
  await dialog.getByRole('button', { name: 'Confirm revoke all' }).click();
  await expect(dialog.getByRole('alert')).toContainText(
    'The result could not be confirmed',
  );
  await expect(
    page.getByText('All sessions revoked.', { exact: true }),
  ).toHaveCount(0);
  expect(requests).toBe(1);
});

for (const width of [360, 768, 1280]) {
  test(`session table scroll and revoke confirmation keyboard focus at ${width}px`, async ({
    page,
  }, testInfo) => {
    await page.setViewportSize({ width, height: 900 });
    await signIn(page);
    await openAccount(page);
    const table = section(page).getByRole('region', {
      name: 'Sessions table · scroll horizontally for all columns',
    });
    await expect(table).toBeVisible();
    await table.focus();
    await page.keyboard.press('ArrowRight');
    await expect
      .poll(() => table.evaluate((element) => element.scrollLeft))
      .toBeGreaterThan(0);
    // Native smooth scrolling can advance more than one step after key release.
    // Use repeated keyboard steps to reach the left edge, without fixed sleeps.
    for (let step = 0; step < 5; step++) await page.keyboard.press('ArrowLeft');
    await expect
      .poll(() => table.evaluate((element) => element.scrollLeft))
      .toBe(0);
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true);
    const trigger = section(page).getByRole('button', {
      name: 'Revoke all sessions',
      exact: true,
    });
    await trigger.focus();
    await page.keyboard.press('Enter');
    const dialog = page.getByRole('dialog', {
      name: 'Revoke all sessions?',
      exact: true,
    });
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
    await page.screenshot({
      path: testInfo.outputPath('sessions.png'),
      fullPage: false,
      animations: 'disabled',
    });
    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
    await expect(trigger).toBeFocused();
  });
}
