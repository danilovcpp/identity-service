import { expect, test, type Page } from '@playwright/test';
import { signIn } from './authFixture';

test.describe.configure({ mode: 'serial' });
const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null;
const auditResponse = (page: Page) =>
  page.waitForResponse(
    (response) =>
      new URL(response.url()).pathname === '/api/v1/admin/audit-events' &&
      response.request().method() === 'GET',
  );
async function readEvents(response: Awaited<ReturnType<typeof auditResponse>>) {
  expect(response.status()).toBe(200);
  const data: unknown = await response.json();
  if (!isRecord(data) || !Array.isArray(data.items))
    throw new Error('Invalid audit fixture response.');
  return data.items.map((item: unknown) => {
    if (
      !isRecord(item) ||
      typeof item.id !== 'string' ||
      typeof item.occurredAt !== 'string' ||
      typeof item.eventType !== 'string'
    )
      throw new Error('Invalid audit fixture item.');
    return {
      id: item.id,
      occurredAt: item.occurredAt,
      eventType: item.eventType,
    };
  });
}

for (const readOnly of [false, true]) {
  test(`${readOnly ? 'ReadOnly' : 'Administrator'} reads real audit rows in API order with matching timestamps`, async ({
    page,
  }) => {
    await signIn(page, readOnly);
    const response = auditResponse(page);
    await page.getByRole('link', { name: 'Audit log', exact: true }).click();
    const items = await readEvents(await response);
    await expect(
      page.getByRole('heading', { name: 'Audit log', exact: true }),
    ).toBeVisible();
    const rows = page.getByRole('table').locator('tbody tr');
    await expect(rows).toHaveCount(items.length);
    for (const [index, item] of items.entries()) {
      await expect(rows.nth(index)).toHaveAttribute('data-row-key', item.id);
      await expect(rows.nth(index).locator('time')).toHaveAttribute(
        'datetime',
        item.occurredAt,
      );
      await expect(rows.nth(index).locator('time')).toHaveAttribute(
        'title',
        new Date(item.occurredAt).toISOString(),
      );
      await expect(rows.nth(index)).toContainText(item.eventType);
    }
    await expect(
      page.getByRole('button', { name: /delete|export|revoke|invite|edit/i }),
    ).toHaveCount(0);
    const link = page.getByRole('link', { name: /^View user / }).first();
    const destination = await link.getAttribute('href');
    await link.click();
    await expect(page).toHaveURL(new RegExp(destination + '$'));
    await expect(
      page.getByRole('region', { name: 'Account information' }),
    ).toBeVisible();
    await expect(
      page.getByRole('list', { name: 'Security events' }),
    ).toBeVisible();
    const userId = new URL(page.url()).pathname.split('/').at(-1);
    if (!userId) throw new Error('Missing linked fixture ID.');
    // Keep this SPA instance: the card caches a projected activity array while
    // Audit log caches a page object, even for identical API query parameters.
    await page.getByRole('link', { name: 'Audit log', exact: true }).click();
    await page.getByRole('combobox', { name: 'Events per page' }).click();
    await page.getByRole('option', { name: '10 / page', exact: true }).click();
    await expect(page).toHaveURL(/\/admin\/audit\?pageSize=10$/);
    await expect(page.getByRole('table').locator('tbody tr')).toHaveCount(10);
    await page
      .getByLabel('Target / related user ID', { exact: true })
      .fill(userId);
    await page.getByRole('button', { name: 'Apply filters' }).click();
    await expect(page.getByRole('table')).toBeVisible();
    await expect(page).toHaveURL(
      new RegExp('pageSize=10&userId=' + userId + '$'),
    );
  });
}

test('all filters are server-side, restore across reload/history, and retain safe login return parameters', async ({
  page,
}) => {
  await signIn(page);
  const profile: unknown = await (
    await page.request.get('/api/v1/admin/auth/me')
  ).json();
  if (!isRecord(profile) || typeof profile.id !== 'string')
    throw new Error('Invalid profile fixture.');
  await page.goto('/admin/audit');
  await expect(page.getByRole('table')).toBeVisible();
  await page.getByLabel('Actor user ID', { exact: true }).fill(profile.id);
  await page
    .getByLabel('Target / related user ID', { exact: true })
    .fill(profile.id);
  await page.getByLabel('Event type', { exact: true }).fill('admin_login');
  await page
    .getByLabel('From · local time (inclusive)')
    .fill('2020-01-01T00:00');
  await page.getByLabel('To · local time (exclusive)').fill('2099-01-01T00:00');
  const response = auditResponse(page);
  await page.getByRole('button', { name: 'Apply filters' }).click();
  const filtered = await response;
  const query = new URL(filtered.url()).searchParams;
  expect(query.get('ActorUserId')).toBe(profile.id);
  expect(query.get('UserId')).toBe(profile.id);
  expect(query.get('EventType')).toBe('admin_login');
  expect(query.get('From')).toMatch(/Z$/);
  expect(query.get('To')).toMatch(/Z$/);
  const items = await readEvents(filtered);
  expect(items.length).toBeGreaterThan(0);
  expect(items.every((item) => item.eventType === 'admin_login')).toBe(true);
  const filteredUrl = page.url();
  await page.reload();
  await expect(page.getByLabel('Event type', { exact: true })).toHaveValue(
    'admin_login',
  );
  await page.getByRole('button', { name: 'Reset filters' }).click();
  await expect(page).toHaveURL(/\/admin\/audit$/);
  await page.goBack();
  await expect(page).toHaveURL(filteredUrl);
  await expect(page.getByLabel('Actor user ID', { exact: true })).toHaveValue(
    profile.id,
  );
  const protectedPath =
    new URL(page.url()).pathname + new URL(page.url()).search;
  await page.getByRole('button', { name: 'Sign out', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Sign in to Identity' }),
  ).toBeVisible();
  await page.goto(protectedPath);
  await expect(
    page.getByRole('heading', { name: 'Sign in to Identity' }),
  ).toBeVisible();
  const password = process.env.IDENTITY_E2E_PASSWORD;
  if (!password) throw new Error('Disposable fixture password is required.');
  await page
    .getByLabel('Email', { exact: true })
    .fill('admin@identity.example');
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page).toHaveURL(filteredUrl);
  await expect(page.getByLabel('Event type', { exact: true })).toHaveValue(
    'admin_login',
  );
});

test('pagination requests another server page and a valid unknown event yields zero results', async ({
  page,
}) => {
  await signIn(page);
  await page.goto('/admin/audit?pageSize=10');
  await expect(page.getByRole('table')).toBeVisible();
  const response = auditResponse(page);
  await page.getByTitle('Next Page').getByRole('button').click();
  const next = await response;
  expect(new URL(next.url()).searchParams.get('Page')).toBe('2');
  expect(new URL(next.url()).searchParams.get('PageSize')).toBe('10');
  await expect(page).toHaveURL(/page=2&pageSize=10$/);
  await page
    .getByLabel('Event type', { exact: true })
    .fill('unknown_event_fixture');
  await page.getByRole('button', { name: 'Apply filters' }).click();
  await expect(
    page.getByRole('heading', { name: 'No audit events found' }),
  ).toBeVisible();
  expect(new URL(page.url()).searchParams.has('page')).toBe(false);
});

test('network errors allow manual GET retry and forbidden responses clear the protected view', async ({
  page,
}) => {
  await signIn(page);
  await page.goto('/admin/audit');
  await expect(page.getByRole('table')).toBeVisible();
  const refresh = page.getByRole('button', { name: 'Refresh audit log' });
  await expect(refresh).not.toHaveClass(/ant-btn-loading/);
  let attempts = 0;
  await page.route('**/api/v1/admin/audit-events?*', (route) => {
    attempts++;
    return route.abort('failed');
  });
  await refresh.click();
  await expect(page.getByRole('alert')).toContainText(
    'Unable to reach Identity',
  );
  expect(attempts).toBe(1);
  await page.unroute('**/api/v1/admin/audit-events?*');
  await page.getByRole('button', { name: 'Try again' }).click();
  await expect(page.getByRole('table')).toBeVisible();
  await expect(refresh).not.toHaveClass(/ant-btn-loading/);
  await page.route('**/api/v1/admin/audit-events?*', (route) =>
    route.fulfill({
      status: 403,
      contentType: 'application/problem+json',
      body: JSON.stringify({ code: 'forbidden' }),
    }),
  );
  await page.getByRole('button', { name: 'Refresh audit log' }).click();
  await expect(
    page.getByRole('heading', { name: 'Access denied', exact: true }),
  ).toBeVisible();
  await expect(page.getByRole('table')).toHaveCount(0);
});

for (const width of [360, 768, 1280]) {
  test(`audit filters, keyboard table scroll and layout at ${width}px`, async ({
    page,
  }, testInfo) => {
    await page.setViewportSize({ width, height: 900 });
    await signIn(page, true);
    await page.goto('/admin/audit?pageSize=10');
    const table = page.getByRole('region', {
      name: 'Audit table · scroll horizontally for all columns',
    });
    await expect(table).toBeVisible();
    await table.focus();
    await page.keyboard.press('ArrowRight');
    await expect
      .poll(() => table.evaluate((element) => element.scrollLeft))
      .toBeGreaterThan(0);
    for (let step = 0; step < 5; step++) await page.keyboard.press('ArrowLeft');
    await expect
      .poll(() => table.evaluate((element) => element.scrollLeft))
      .toBe(0);
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true);
    await page
      .getByRole('heading', { name: 'Audit log', exact: true })
      .scrollIntoViewIfNeeded();
    await page.screenshot({
      path: testInfo.outputPath('audit.png'),
      fullPage: true,
      animations: 'disabled',
    });
    expect(
      await page.evaluate(() => ({
        local: localStorage.length,
        session: sessionStorage.length,
      })),
    ).toEqual({ local: 0, session: 0 });
  });
}
