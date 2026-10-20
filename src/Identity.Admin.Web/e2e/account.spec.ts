import { expect, test, type Page } from '@playwright/test';
import { signIn } from './authFixture';

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null;
const syntheticToken = 'N'.repeat(43);
const activation = '/account/activate';
const reset = '/account/reset-password';

// Secret-bearing operations use Node fetch/closures and sanitized failures.
// Traces/video are disabled; only empty, fragment-free forms are screenshotted.
async function jsonRequest(path: string, body: unknown) {
  try {
    return await fetch(new URL(path, process.env.IDENTITY_E2E_BASE_URL), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
      redirect: 'error',
    });
  } catch {
    throw new Error('Disposable account API request failed.');
  }
}
async function visit(page: Page, path: string, token = syntheticToken) {
  try {
    await page.goto(path + '#token=' + encodeURIComponent(token));
  } catch {
    throw new Error('Account page navigation failed.');
  }
  await expect.poll(() => new URL(page.url()).hash === '').toBe(true);
}
async function fill(page: Page, password: string, confirmation = password) {
  try {
    await page.getByLabel('New password', { exact: true }).fill(password);
    await page
      .getByLabel('Confirm password', { exact: true })
      .fill(confirmation);
  } catch {
    throw new Error('Could not fill the account password form.');
  }
}
async function submit(
  page: Page,
  path: string,
  password: string,
  status: number,
) {
  await fill(page, password);
  const response = page.waitForResponse(
    (res) =>
      res.request().method() === 'POST' &&
      res
        .url()
        .toLowerCase()
        .endsWith(
          path === activation
            ? '/auth/invitations/accept'
            : '/auth/password/reset',
        ),
  );
  await page
    .getByRole('button', {
      name: path === activation ? 'Activate account' : 'Reset password',
      exact: true,
    })
    .click();
  const result = await response;
  expect(result.status()).toBe(status);
  expect(Boolean(result.request().headers()['x-csrf-token'])).toBe(false);
  expect(new URL(result.url()).search).toBe('');
}
async function csrf(page: Page) {
  const data: unknown = await (
    await page.request.get('/api/v1/admin/auth/csrf')
  ).json();
  if (!isRecord(data) || typeof data.token !== 'string')
    throw new Error('Invalid admin fixture.');
  return { 'X-CSRF-TOKEN': data.token };
}
async function invite(page: Page, email: string) {
  const headers = await csrf(page);
  expect(
    (
      await page.request.post('/api/v1/admin/users/invitations', {
        headers,
        data: { email, displayName: 'Public account flow' },
      })
    ).status(),
  ).toBe(202);
  const data: unknown = await (
    await page.request.get(
      '/api/v1/admin/users?Search=' + encodeURIComponent(email),
    )
  ).json();
  if (!isRecord(data) || !Array.isArray(data.items))
    throw new Error('Invalid users fixture.');
  const account: unknown = data.items.find(
    (item: unknown) => isRecord(item) && item.email === email,
  );
  if (!isRecord(account) || typeof account.id !== 'string')
    throw new Error('Missing invited account.');
  return account.id;
}
async function emailToken(email: string, subject: RegExp) {
  const mailpit = process.env.IDENTITY_E2E_MAILPIT_URL;
  if (!mailpit) throw new Error('Mailpit fixture is required.');
  let messageId: string | undefined;
  await expect
    .poll(async () => {
      const data: unknown = await (
        await fetch(mailpit + '/api/v1/messages')
      ).json();
      if (!isRecord(data) || !Array.isArray(data.messages))
        throw new Error('Invalid mail envelope.');
      const message: unknown = data.messages.find(
        (value: unknown) =>
          isRecord(value) &&
          typeof value.Subject === 'string' &&
          subject.test(value.Subject) &&
          Array.isArray(value.To) &&
          value.To.some(
            (recipient: unknown) =>
              isRecord(recipient) && recipient.Address === email,
          ),
      );
      messageId =
        isRecord(message) && typeof message.ID === 'string'
          ? message.ID
          : undefined;
      return Boolean(messageId);
    })
    .toBe(true);
  const data: unknown = await (
    await fetch(mailpit + '/api/v1/message/' + messageId)
  ).json();
  const match =
    isRecord(data) && typeof data.Text === 'string'
      ? /#token=([A-Za-z0-9_-]{43,512})/.exec(data.Text)
      : null;
  if (!match?.[1]) throw new Error('Missing email account fragment.');
  return match[1];
}
async function storageIsEmpty(page: Page) {
  expect(
    await page.evaluate(() => localStorage.length + sessionStorage.length),
  ).toBe(0);
}

test.afterEach(async ({ page }) => {
  // Do not leave password values in Playwright's failure-context snapshot.
  if (!page.isClosed())
    await page
      .evaluate(() => {
        document
          .querySelectorAll<HTMLInputElement>('input[type=password]')
          .forEach((input) => {
            input.value = '';
          });
        if (location.hash)
          history.replaceState(
            history.state,
            '',
            location.pathname + location.search,
          );
      })
      .catch(() => undefined);
});

test('Mailpit invitation activation and password reset work anonymously, reject replay and revoke the old refresh session', async ({
  page,
  browser,
}) => {
  const email = `zz-public-${crypto.randomUUID()}@identity.example`;
  const password = 'Account initial ' + crypto.randomUUID();
  const newPassword = 'Account changed ' + crypto.randomUUID();
  await signIn(page);
  const userId = await invite(page, email);
  const invitation = await emailToken(email, /invitation/i);
  const context = await browser.newContext({
    baseURL: process.env.IDENTITY_E2E_BASE_URL,
    ignoreHTTPSErrors: true,
  });
  try {
    const account = await context.newPage();
    let adminRequests = 0;
    let secretInConsole = false;
    const secrets = [invitation, password, newPassword];
    account.on('request', (request) => {
      if (request.url().includes('/api/v1/admin/')) adminRequests++;
    });
    account.on('console', (message) => {
      if (secrets.some((secret) => message.text().includes(secret)))
        secretInConsole = true;
    });
    await visit(account, activation, invitation);
    await expect(
      account.getByRole('heading', { name: 'Activate your account' }),
    ).toBeVisible();
    expect(
      await account.evaluate(
        (secret) => JSON.stringify(history.state).includes(secret),
        invitation,
      ),
    ).toBe(false);
    await storageIsEmpty(account);
    await submit(account, activation, password, 204);
    await expect(
      account.getByText('Account activated', { exact: true }),
    ).toBeVisible();
    await expect(
      account.getByRole('link', { name: 'Administrator sign in' }),
    ).toHaveAttribute('href', '/admin/login');
    expect(
      (await context.cookies()).some((cookie) => /admin/i.test(cookie.name)),
    ).toBe(false);
    await visit(account, activation, invitation);
    await submit(account, activation, password, 400);
    await expect(account.getByText('Link unavailable')).toBeVisible();
    const login = await jsonRequest('/api/v1/auth/login', {
      email,
      password,
      clientId: 'native',
    });
    expect(login.status).toBe(200);
    const session: unknown = await login.json();
    if (!isRecord(session) || typeof session.refreshToken !== 'string')
      throw new Error('Invalid native session.');
    expect(
      (
        await page.request.post(
          `/api/v1/admin/users/${userId}/password-reset`,
          { headers: await csrf(page) },
        )
      ).status(),
    ).toBe(202);
    const recovery = await emailToken(email, /reset/i);
    secrets.push(recovery);
    await visit(account, reset, recovery);
    await expect(
      account.getByRole('heading', { name: 'Choose a new password' }),
    ).toBeVisible();
    await submit(account, reset, newPassword, 204);
    await expect(
      account.getByText('Password changed', { exact: true }),
    ).toBeVisible();
    expect(
      (
        await jsonRequest('/api/v1/auth/refresh', {
          refreshToken: session.refreshToken,
        })
      ).status,
    ).toBe(401);
    expect(
      (
        await jsonRequest('/api/v1/auth/login', {
          email,
          password,
          clientId: 'native',
        })
      ).status,
    ).toBe(401);
    expect(
      (
        await jsonRequest('/api/v1/auth/login', {
          email,
          password: newPassword,
          clientId: 'native',
        })
      ).status,
    ).toBe(200);
    await visit(account, reset, recovery);
    await submit(account, reset, newPassword, 400);
    await expect(account.getByText('Link unavailable')).toBeVisible();
    expect(adminRequests).toBe(0);
    expect(secretInConsole).toBe(false);
    await storageIsEmpty(account);
  } finally {
    await context.close();
  }
});

test('missing/malformed links are explicit and reload cannot restore the consumed fragment', async ({
  page,
}) => {
  let requests = 0;
  page.on('request', (request) => {
    if (request.url().includes('/api/v1/')) requests++;
  });
  await page.goto(reset);
  await expect(page.getByText('Link required')).toBeVisible();
  await visit(page, '/%61ccount/activate/');
  await expect(page.getByLabel('New password', { exact: true })).toBeVisible();
  await visit(page, activation, 'malformed');
  await expect(page.getByText('Link unavailable')).toBeVisible();
  await visit(page, reset);
  await expect(page.getByLabel('New password', { exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByText('Link required')).toBeVisible();
  expect(requests).toBe(0);
  await storageIsEmpty(page);
});

test('client validation, real server policy and invalid-token responses never expose the token', async ({
  page,
}) => {
  await visit(page, activation);
  await fill(page, 'short');
  await page.getByRole('button', { name: 'Activate account' }).click();
  await expect(
    page.getByText('Use 15–128 Unicode characters after normalization.'),
  ).toBeVisible();
  await fill(page, 'correcthorsebatterystaple', 'different');
  await page.getByRole('button', { name: 'Activate account' }).click();
  await expect(page.getByText('The passwords do not match.')).toBeVisible();
  await submit(page, activation, 'correcthorsebatterystaple', 400);
  await expect(page.getByText(/choose a less common password/)).toBeVisible();
  await expect(page.getByLabel('New password', { exact: true })).toHaveValue(
    '',
  );
  await submit(page, activation, 'Supported uncommon passphrase 89', 400);
  await expect(page.getByText('Link unavailable')).toBeVisible();
  await storageIsEmpty(page);
});

test('network uncertainty permits explicit retry and does not automatically repeat a password write', async ({
  page,
}) => {
  await visit(page, reset);
  let posts = 0;
  await page.route('**/api/v1/Auth/password/reset', (route) => {
    posts++;
    return route.abort('failed');
  });
  await fill(page, 'Supported uncommon passphrase 89');
  await page.getByRole('button', { name: 'Reset password' }).click();
  await expect(
    page.getByText(/The result could not be confirmed/),
  ).toBeVisible();
  await expect(page.getByLabel('New password', { exact: true })).toHaveValue(
    '',
  );
  expect(posts).toBe(1);
  await storageIsEmpty(page);
});

for (const width of [360, 768, 1280]) {
  test(`public form supports keyboard focus and fits ${width}px without leaking a fragment`, async ({
    page,
  }, testInfo) => {
    await page.setViewportSize({ width, height: 900 });
    await visit(page, activation);
    const password = page.getByLabel('New password', { exact: true });
    await expect(password).toBeVisible();
    await password.focus();
    await expect(password).toBeFocused();
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');
    await expect(
      page.getByLabel('Confirm password', { exact: true }),
    ).toBeFocused();
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
    expect(new URL(page.url()).hash).toBe('');
    await page.screenshot({
      path: testInfo.outputPath('public-account.png'),
      fullPage: true,
      animations: 'disabled',
    });
  });
}
