import { expect, test } from '@playwright/test';
import { signIn } from './authFixture';

for (const readOnly of [false, true]) {
  test(`${readOnly ? 'ReadOnly' : 'Administrator'} reads the real filtered users catalog`, async ({
    page,
  }) => {
    await signIn(page, readOnly);
    const requests: string[] = [];
    page.on('request', (request) => {
      if (new URL(request.url()).pathname === '/api/v1/admin/users')
        requests.push(request.method());
    });
    await page.goto(
      '/admin/users?search=admin%40identity.example&status=Active&emailVerified=true',
    );
    await expect(page.getByText('1 user found', { exact: true })).toBeVisible();
    await expect(
      page.getByRole('cell', { name: 'admin@identity.example', exact: true }),
    ).toBeVisible();
    await expect(
      page.getByRole('cell', { name: 'Verified', exact: true }),
    ).toBeVisible();
    await expect(page.locator('time')).toHaveAttribute('title', /Z$/);
    const link = page.getByRole('link', {
      name: 'View details for admin@identity.example',
    });
    await expect(link).toHaveAttribute(
      'href',
      /^\/admin\/users\/[0-9a-f-]{36}$/,
    );
    await expect(
      page.getByRole('button', {
        name: /invite|disable|enable|reset password|revoke/i,
      }),
    ).toHaveCount(0);
    expect(requests.length).toBeGreaterThan(0);
    expect(requests.every((method) => method === 'GET')).toBe(true);
  });
}

test('server pagination, search debounce, filters and back/forward restore the URL', async ({
  page,
}) => {
  await signIn(page, true);
  await page.goto('/admin/users?pageSize=1&sortBy=email&sortDirection=asc');
  await expect(
    page.getByRole('cell', { name: 'admin@identity.example', exact: true }),
  ).toBeVisible();
  const response = page.waitForResponse(
    (res) => new URL(res.url()).searchParams.get('Page') === '2',
  );
  await page.getByTitle('Next Page').click();
  expect((await response).status()).toBe(200);
  await expect(page).toHaveURL(/page=2/);
  await expect(page.getByRole('table')).toBeVisible();
  await page.goBack();
  await expect(page).not.toHaveURL(/page=2/);
  await expect(
    page.getByRole('cell', { name: 'admin@identity.example', exact: true }),
  ).toBeVisible();
  await page.goForward();
  await expect(page).toHaveURL(/page=2/);
  const searches: string[] = [];
  page.on('request', (request) => {
    const url = new URL(request.url());
    if (
      url.pathname === '/api/v1/admin/users' &&
      url.searchParams.has('Search')
    )
      searches.push(url.searchParams.get('Search') ?? '');
  });
  const search = page.getByRole('textbox', { name: 'Search users' });
  await search.fill('rea');
  await search.fill('reader@identity.example');
  await expect(page.getByText('1 user found', { exact: true })).toBeVisible();
  await expect(
    page.getByRole('cell', { name: 'reader@identity.example', exact: true }),
  ).toBeVisible();
  expect(searches).toEqual(['reader@identity.example']);
  await expect(search).toBeFocused();
  await expect(page).not.toHaveURL(/page=2/);
  await test.step('select Disabled using the keyboard', async () => {
    const status = page.getByRole('combobox', { name: 'Status', exact: true });
    await status.click();
    for (let index = 0; index < 4; index++) await status.press('ArrowDown');
    await status.press('Enter');
  });
  await expect(
    page.getByRole('heading', { name: 'No users found' }),
  ).toBeVisible();
  await expect(page).toHaveURL(/status=Disabled/);
  await page.getByRole('button', { name: 'Reset filters' }).click();
  await expect(page).toHaveURL(/\/admin\/users$/);
  await expect(search).toHaveValue('');
  await expect(page.getByRole('table')).toBeVisible();
});

test('network failure has an explicit manual GET retry', async ({ page }) => {
  await signIn(page);
  await page.route(
    (url) => url.pathname === '/api/v1/admin/users',
    (route) => route.abort('failed'),
  );
  await page.goto('/admin/users');
  await expect(page.getByRole('alert')).toBeVisible();
  await page.unrouteAll({ behavior: 'wait' });
  await page.getByRole('button', { name: 'Try again' }).click();
  await expect(page.getByRole('table')).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
});

for (const width of [360, 768, 1280]) {
  test(`users at ${width}px keep horizontal scrolling inside the table`, async ({
    page,
  }, info) => {
    await signIn(page, true);
    await page.setViewportSize({ width, height: 900 });
    await page.goto('/admin/users');
    await expect(page.getByRole('table')).toBeVisible();
    const region = page.getByRole('region', { name: /Users table/ });
    await region.focus();
    await page.keyboard.press('ArrowRight');
    if (width < 1280)
      await expect
        .poll(() => region.evaluate((element) => element.scrollLeft))
        .toBeGreaterThan(0);
    await page.keyboard.press('ArrowLeft');
    await expect
      .poll(() => region.evaluate((element) => element.scrollLeft))
      .toBe(0);
    await expect
      .poll(() => page.evaluate(() => document.documentElement.scrollWidth))
      .toBe(width);
    await expect(
      page.getByRole('textbox', { name: 'Search users' }),
    ).toBeVisible();
    await expect(
      page.getByRole('combobox', { name: 'Rows per page' }),
    ).toBeVisible();
    expect(
      await page.evaluate(() => localStorage.length + sessionStorage.length),
    ).toBe(0);
    await page.screenshot({
      path: info.outputPath('users.png'),
      fullPage: true,
    });
  });
}

test('anonymous users return to the validated catalog filters after login', async ({
  page,
}) => {
  const password = process.env.IDENTITY_E2E_PASSWORD;
  if (!password) throw new Error('Disposable fixture password is required.');
  await page.goto('/admin/users?search=reader&status=Active&token=discarded');
  await expect(page).toHaveURL(/\/admin\/login$/);
  await page
    .getByLabel('Email', { exact: true })
    .fill('reader@identity.example');
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/users\?search=reader&status=Active$/);
  await expect(
    page.getByRole('cell', { name: 'reader@identity.example', exact: true }),
  ).toBeVisible();
});
