import { expect, test } from '@playwright/test';
import { signIn } from './authFixture';

test('anonymous protected routes require a session and invalid credentials stay generic', async ({
  page,
}, info) => {
  await page.goto('/admin/users');
  await expect(page).toHaveURL(/\/admin\/login$/);
  await expect(page.getByRole('navigation')).toHaveCount(0);
  for (const width of [360, 768, 1280]) {
    await page.setViewportSize({ width, height: 900 });
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
    await page.screenshot({
      path: info.outputPath(`login-${width}.png`),
      fullPage: true,
    });
  }
  await page
    .getByLabel('Email', { exact: true })
    .fill('absent@identity.example');
  await page
    .getByLabel('Password', { exact: true })
    .fill('Incorrect test-only passphrase');
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText(
    'Unable to sign in with these credentials.',
  );
  // No screenshots after credential entry, tracing, storage export or body logging.
  expect(await page.getByLabel('Password', { exact: true }).inputValue()).toBe(
    '',
  );
});

for (const readOnly of [false, true]) {
  test(`${readOnly ? 'ReadOnly' : 'Administrator'} login, cookie refresh and logout`, async ({
    page,
    context,
  }) => {
    const errors: string[] = [];
    page.on('pageerror', (error) => errors.push(error.name));
    await signIn(page, readOnly);
    await expect(
      page.getByText(readOnly ? 'Read only' : 'Administrator', { exact: true }),
    ).toBeVisible();
    const cookies = await context.cookies();
    const auth = cookies.find(
      (cookie) => cookie.name === '__Host-Identity.Admin',
    );
    expect(
      Boolean(auth?.httpOnly && auth.secure && auth.sameSite === 'Strict'),
    ).toBe(true);
    expect(
      await page.evaluate(() => localStorage.length + sessionStorage.length),
    ).toBe(0);
    expect(
      await page.evaluate(() =>
        document.cookie.includes('__Host-Identity.Admin'),
      ),
    ).toBe(false);
    await page.reload();
    await expect(
      page.getByRole('heading', { name: 'Identity Console', exact: true }),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Sign out' }).click();
    await expect(
      page.getByRole('button', { name: 'Sign in', exact: true }),
    ).toBeVisible();
    await page.goto('/admin');
    await expect(page).toHaveURL(/\/admin\/login$/);
    expect(errors).toEqual([]);
    const response = await page.request.get('/api/v1/admin/auth/me');
    expect(response.status()).toBe(401);
  });
}

test('server revocation rejects the old cookie on the next page refresh', async ({
  page,
  context,
}) => {
  await signIn(page);
  const oldCookie = (await context.cookies()).find(
    (cookie) => cookie.name === '__Host-Identity.Admin',
  );
  if (!oldCookie) throw new Error('Administrative cookie was not issued.');
  const csrf = await page.request.get('/api/v1/admin/auth/csrf');
  const body: unknown = await csrf.json();
  if (
    typeof body !== 'object' ||
    body === null ||
    !('token' in body) ||
    typeof body.token !== 'string'
  )
    throw new Error('Invalid fixture CSRF response.');
  const response = await page.request.post('/api/v1/admin/auth/logout', {
    headers: { 'X-CSRF-TOKEN': body.token },
  });
  expect(response.status()).toBe(204);
  // Restore the old ticket only in memory: the database revocation must reject it.
  await context.addCookies([oldCookie]);
  await page.reload();
  await expect(page).toHaveURL(/\/admin\/login$/);
  await expect(page.getByRole('navigation')).toHaveCount(0);
});
