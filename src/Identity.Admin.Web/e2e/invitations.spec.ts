import { test } from './fixture';
import { expect, type Page } from '@playwright/test';
import { signIn } from './authFixture';

test.describe.configure({ mode: 'serial' });
const email = `zz-invited-${crypto.randomUUID()}@identity.example`;
let accountPath = '';
const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null;

async function deliveredInvitations(page: Page) {
  const mailpit = process.env.IDENTITY_E2E_MAILPIT_URL;
  if (!mailpit) throw new Error('Disposable Mailpit fixture is required.');
  const response = await page.request.get(`${mailpit}/api/v1/messages`);
  expect(response.status()).toBe(200);
  const data: unknown = await response.json();
  if (!isRecord(data) || !Array.isArray(data.messages))
    throw new Error('Invalid Mailpit response.');
  // Inspect envelope metadata only: never load or print a body containing a link/token.
  return data.messages.filter(
    (item: unknown) =>
      isRecord(item) &&
      Array.isArray(item.To) &&
      item.To.some(
        (recipient: unknown) =>
          isRecord(recipient) && recipient.Address === email,
      ) &&
      typeof item.Subject === 'string' &&
      /invitation/i.test(item.Subject),
  ).length;
}
async function openInvite(page: Page) {
  await page.goto('/admin/users');
  await page.getByRole('button', { name: 'Invite user', exact: true }).click();
  return page.getByRole('dialog', { name: 'Invite user', exact: true });
}

test('Administrator invites a user, sees Invited, resends after confirmation and Mailpit receives both requests', async ({
  page,
}) => {
  await signIn(page);
  await page.goto('/admin/users?search=' + encodeURIComponent(email));
  await expect(page.getByText('No users found', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Invite user', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Invite user', exact: true });
  await dialog.getByLabel('Email', { exact: true }).fill(email);
  await dialog.getByLabel('Display name (optional)').fill('Invited colleague');
  const accepted = page.waitForResponse(
    (res) =>
      res.url().endsWith('/api/v1/admin/users/invitations') &&
      res.request().method() === 'POST',
  );
  await dialog
    .getByRole('button', { name: 'Send invitation', exact: true })
    .click();
  expect((await accepted).status()).toBe(202);
  await expect(dialog).toBeHidden();
  await expect(
    page.getByText('Invitation accepted for processing.', { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole('cell', { name: 'Invited', exact: true }),
  ).toBeVisible();
  await expect.poll(() => deliveredInvitations(page)).toBe(1);
  await page.getByRole('link', { name: `View details for ${email}` }).click();
  await expect(
    page.getByRole('region', { name: 'Account information' }),
  ).toBeVisible();
  accountPath = new URL(page.url()).pathname;
  const commands: string[] = [];
  page.on('request', (request) => {
    if (request.url().endsWith('/invitations/resend'))
      commands.push(request.method());
  });
  const resend = page.getByRole('button', {
    name: 'Resend invitation',
    exact: true,
  });
  await resend.click();
  const confirm = page.getByRole('dialog', { name: 'Resend invitation?' });
  await expect(confirm).toBeVisible();
  expect(commands).toEqual([]);
  await confirm.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(confirm).toBeHidden();
  await expect(resend).toBeFocused();
  await resend.click();
  const resent = page.waitForResponse(
    (res) =>
      res.url().endsWith('/invitations/resend') &&
      res.request().method() === 'POST',
  );
  await confirm
    .getByRole('button', { name: 'Request another invitation' })
    .click();
  expect((await resent).status()).toBe(202);
  await expect(confirm).toBeHidden();
  await expect(
    page.getByText('admin_resent_invitation', { exact: true }),
  ).toBeVisible();
  await expect.poll(() => deliveredInvitations(page)).toBe(2);
  expect(commands).toEqual(['POST']);
  expect(
    await page.evaluate(() => ({
      local: localStorage.length,
      session: sessionStorage.length,
    })),
  ).toEqual({ local: 0, session: 0 });
});

test('ReadOnly sees the invited account without either write action', async ({
  page,
}) => {
  await signIn(page, true);
  await page.goto('/admin/users?search=' + encodeURIComponent(email));
  await expect(
    page.getByRole('cell', { name: 'Invited', exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole('button', { name: 'Invite user', exact: true }),
  ).toHaveCount(0);
  await page.getByRole('link', { name: `View details for ${email}` }).click();
  await expect(
    page.getByRole('region', { name: 'Account information' }),
  ).toBeVisible();
  await expect(
    page.getByRole('button', { name: 'Resend invitation', exact: true }),
  ).toHaveCount(0);
});

test('duplicate invite returns real 409, preserves input and makes only one POST', async ({
  page,
}) => {
  await signIn(page);
  const dialog = await openInvite(page);
  await dialog
    .getByLabel('Email', { exact: true })
    .fill('admin@identity.example');
  let posts = 0;
  page.on('request', (request) => {
    if (
      request.url().endsWith('/users/invitations') &&
      request.method() === 'POST'
    )
      posts++;
  });
  await dialog.getByRole('button', { name: 'Send invitation' }).click();
  await expect(dialog.getByRole('alert')).toContainText(
    'An account with this email already exists',
  );
  await expect(dialog.getByLabel('Email', { exact: true })).toHaveValue(
    'admin@identity.example',
  );
  expect(posts).toBe(1);
});

test('network failure never repeats resend or claims acceptance', async ({
  page,
}) => {
  await signIn(page);
  await page.goto(accountPath);
  let posts = 0;
  await page.route('**/invitations/resend', (route) => {
    posts++;
    return route.abort('failed');
  });
  await page.getByRole('button', { name: 'Resend invitation' }).click();
  const dialog = page.getByRole('dialog', { name: 'Resend invitation?' });
  await dialog
    .getByRole('button', { name: 'Request another invitation' })
    .click();
  await expect(dialog.getByRole('alert')).toContainText(
    'The result could not be confirmed',
  );
  await expect(
    page.getByText('Invitation accepted for processing.', { exact: true }),
  ).toHaveCount(0);
  expect(posts).toBe(1);
});

for (const width of [360, 768, 1280]) {
  test(`invitation modal keyboard navigation and focus return at ${width}px`, async ({
    page,
  }, testInfo) => {
    await page.setViewportSize({ width, height: 900 });
    await signIn(page);
    await page.goto('/admin/users');
    const trigger = page.getByRole('button', {
      name: 'Invite user',
      exact: true,
    });
    await trigger.focus();
    await page.keyboard.press('Enter');
    const dialog = page.getByRole('dialog', {
      name: 'Invite user',
      exact: true,
    });
    await expect(dialog.getByLabel('Email', { exact: true })).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(dialog.getByLabel('Display name (optional)')).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(
      dialog.getByRole('button', { name: 'Cancel', exact: true }),
    ).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(
      dialog.getByRole('button', { name: 'Send invitation' }),
    ).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(dialog.getByRole('button', { name: 'Close' })).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(dialog.getByLabel('Email', { exact: true })).toBeFocused();
    await expect(dialog.locator('input[type=password]')).toHaveCount(0);
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true);
    await page.screenshot({
      path: testInfo.outputPath('invite-user.png'),
      fullPage: true,
    });
    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
    await expect(trigger).toBeFocused();
  });
}
