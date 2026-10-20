import { test } from './fixture';
import { expect } from '@playwright/test';
import { signIn } from './authFixture';
// The registration scenario mutates this run's shared, disposable database.
test.describe.configure({ mode: 'serial' });

function count(data: unknown, field: string): number {
  if (typeof data !== 'object' || data === null || !(field in data))
    throw new Error('Dashboard field is missing.');
  const value: unknown = Reflect.get(data, field);
  if (typeof value !== 'number' && typeof value !== 'string')
    throw new Error('Invalid dashboard count.');
  const result = Number(value);
  if (!Number.isSafeInteger(result) || result < 0)
    throw new Error('Invalid dashboard count.');
  return result;
}

for (const readOnly of [false, true]) {
  test(`${readOnly ? 'ReadOnly' : 'Administrator'} reads real dashboard counts`, async ({
    page,
  }) => {
    await signIn(page, readOnly);
    const response = await page.request.get('/api/v1/admin/dashboard');
    expect(response.status()).toBe(200);
    const data: unknown = await response.json();
    for (const [field, label] of [
      ['totalUsers', 'Total users'],
      ['activeUsers', 'Active users'],
      ['pendingVerificationUsers', 'Pending verification'],
      ['invitedUsers', 'Invited'],
      ['disabledUsers', 'Disabled'],
      ['deactivatedUsers', 'Deactivated'],
      ['activeSessions', 'Active sessions'],
    ]) {
      if (!field || !label) throw new Error('Invalid test metric.');
      await expect(
        page
          .getByRole('region', { name: label, exact: true })
          .getByText(
            new Intl.NumberFormat('en-US').format(count(data, field)),
            { exact: true },
          ),
      ).toBeVisible();
    }
    await expect(
      page.getByRole('button', { name: /invite|disable|reset/i }),
    ).toHaveCount(0);
    const chart = page.getByRole('application');
    await chart.focus();
    await page.keyboard.press('ArrowRight');
    await expect(page.locator('.recharts-tooltip-wrapper')).toContainText(
      '(UTC)',
    );
    await page.getByText('View daily values', { exact: true }).click();
    await expect(page.getByRole('table').getByRole('row')).toHaveCount(31);
  });
}

test('registration through the existing API appears after dashboard refresh', async ({
  page,
}) => {
  await signIn(page);
  await expect(
    page.getByRole('region', { name: 'Total users', exact: true }),
  ).toBeVisible();
  const beforeResponse = await page.request.get('/api/v1/admin/dashboard');
  const before: unknown = await beforeResponse.json();
  const password = process.env.IDENTITY_E2E_PASSWORD;
  if (!password) throw new Error('Disposable fixture password is required.');
  const registered = await page.request.post('/api/v1/auth/register', {
    data: {
      email: `dashboard-${crypto.randomUUID()}@identity.example`,
      password,
      clientId: 'native',
    },
  });
  expect(registered.status()).toBe(202);
  await page.getByRole('button', { name: 'Refresh dashboard' }).click();
  await expect(
    page
      .getByRole('region', { name: 'Total users', exact: true })
      .getByText(
        new Intl.NumberFormat('en-US').format(count(before, 'totalUsers') + 1),
        { exact: true },
      ),
  ).toBeVisible();
  await expect(
    page
      .getByRole('region', { name: 'Pending verification', exact: true })
      .getByText(
        new Intl.NumberFormat('en-US').format(
          count(before, 'pendingVerificationUsers') + 1,
        ),
        { exact: true },
      ),
  ).toBeVisible();
});
