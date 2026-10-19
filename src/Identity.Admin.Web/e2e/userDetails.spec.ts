import { expect, test, type Page } from '@playwright/test';
import { signIn } from './authFixture';

async function openAccount(page: Page, email = 'admin@identity.example') {
  await page.goto(
    '/admin/users?search=' +
      encodeURIComponent(email) +
      '&status=Active&sortBy=email&sortDirection=asc',
  );
  const link = page.getByRole('link', { name: `View details for ${email}` });
  await expect(link).toBeVisible();
  await link.click();
  await expect(
    page.getByRole('region', { name: 'Account information' }),
  ).toBeVisible();
}

for (const readOnly of [false, true]) {
  test(`${readOnly ? 'ReadOnly' : 'Administrator'} opens a real account, copies ID and returns to URL filters`, async ({
    page,
    context,
  }) => {
    await signIn(page, readOnly);
    const navigations: string[] = [];
    const commands: string[] = [];
    await page.goto(
      '/admin/users?search=admin%40identity.example&status=Active&sortBy=email&sortDirection=asc',
    );
    await expect(
      page.getByRole('link', {
        name: 'View details for admin@identity.example',
      }),
    ).toBeVisible();
    page.on('request', (request) => {
      if (request.isNavigationRequest()) navigations.push(request.method());
      if (
        new URL(request.url()).pathname.startsWith('/api/v1/admin/') &&
        request.method() !== 'GET'
      )
        commands.push(request.method());
    });
    await page
      .getByRole('link', { name: 'View details for admin@identity.example' })
      .click();
    const profile = page.getByRole('region', { name: 'Account information' });
    await expect(
      profile.getByText('admin@identity.example', { exact: true }),
    ).toBeVisible();
    await expect(profile.getByText('Verified', { exact: true })).toBeVisible();
    await expect(
      profile.getByText('Not provided', { exact: true }),
    ).toBeVisible();
    await expect(profile.locator('time').first()).toHaveAttribute(
      'title',
      /Z$/,
    );
    await expect(
      page.getByRole('list', { name: 'Security events' }),
    ).toBeVisible();
    const userId = new URL(page.url()).pathname.split('/').at(-1);
    expect(userId).toMatch(/^[0-9a-f-]{36}$/);
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    const copy = page.getByRole('button', { name: 'Copy user ID' });
    await copy.focus();
    await page.keyboard.press('Enter');
    await expect(
      page.getByText('User ID copied.', { exact: true }),
    ).toBeVisible();
    expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(
      userId,
    );
    await expect(
      page.getByRole('button', {
        name: /invite|resend|disable|enable|password|revoke|edit/i,
      }),
    ).toHaveCount(0);
    expect(commands).toEqual([]);
    expect(navigations).toEqual([]);
    page.removeAllListeners('request');
    await page.reload();
    await expect(profile).toBeVisible();
    await page.getByRole('link', { name: 'Back to users' }).click();
    await expect(page).toHaveURL(
      /\/admin\/users\?search=admin%40identity.example&status=Active&sortBy=email&sortDirection=asc$/,
    );
    await expect(
      page.getByRole('textbox', { name: 'Search users' }),
    ).toHaveValue('admin@identity.example');
    await expect(
      page.getByRole('cell', { name: 'admin@identity.example', exact: true }),
    ).toBeVisible();
  });
}

test('invalid UUID and missing account have explicit states without loading unrelated audit', async ({
  page,
}) => {
  await signIn(page);
  const details: string[] = [];
  const activity: string[] = [];
  page.on('request', (request) => {
    const path = new URL(request.url()).pathname;
    if (/\/api\/v1\/admin\/users\//.test(path)) details.push(path);
    if (path === '/api/v1/admin/audit-events') activity.push(path);
  });
  await page.goto('/admin/users/not-a-uuid?search=reader');
  await expect(
    page.getByRole('heading', { name: 'Invalid user ID' }),
  ).toBeVisible();
  expect(details).toEqual([]);
  await page.goto('/admin/users/' + crypto.randomUUID() + '?search=reader');
  await expect(
    page.getByRole('heading', { name: 'User not found' }),
  ).toBeVisible();
  expect(activity).toEqual([]);
  await page.getByRole('link', { name: 'Back to users' }).click();
  await expect(page.getByRole('textbox', { name: 'Search users' })).toHaveValue(
    'reader',
  );
});

test('activity failure leaves the account visible and supports manual GET retry', async ({
  page,
}) => {
  await signIn(page, true);
  await page.route(
    (url) => url.pathname === '/api/v1/admin/audit-events',
    (route) => route.abort('failed'),
  );
  await openAccount(page);
  const activity = page.getByRole('region', {
    name: 'Recent security activity',
  });
  await expect(activity.getByRole('alert')).toBeVisible();
  await expect(
    page.getByRole('region', { name: 'Account information' }),
  ).toBeVisible();
  await page.unrouteAll({ behavior: 'wait' });
  await activity.getByRole('button', { name: 'Try again' }).click();
  await expect(
    activity.getByRole('list', { name: 'Security events' }),
  ).toBeVisible();
});

for (const width of [360, 768, 1280]) {
  test(`account and recent activity fit ${width}px without page overflow`, async ({
    page,
  }, info) => {
    await signIn(page, true);
    await page.setViewportSize({ width, height: 900 });
    await openAccount(page);
    await expect(
      page.getByRole('list', { name: 'Security events' }),
    ).toBeVisible();
    await expect
      .poll(() => page.evaluate(() => document.documentElement.scrollWidth))
      .toBe(width);
    expect(
      await page.evaluate(() => localStorage.length + sessionStorage.length),
    ).toBe(0);
    await page.screenshot({
      path: info.outputPath('user-details.png'),
      fullPage: true,
    });
  });
}
