import AxeBuilder from '@axe-core/playwright';
import { writeFile } from 'node:fs/promises';
import {
  expect,
  type Locator,
  type Page,
  type TestInfo,
} from '@playwright/test';
import { test } from './fixture';
import { signIn } from './authFixture';

const reader = 'reader@identity.example';
async function tabTo(page: Page, target: Locator) {
  for (let step = 0; step < 80; step++) {
    if (await target.evaluate((element) => element === document.activeElement))
      return;
    await page.keyboard.press('Tab');
  }
  throw new Error('The keyboard could not reach the requested control.');
}
async function audit(page: Page, info: TestInfo, name: string) {
  // Audit a stable state: hover portals and an entering modal's partial opacity
  // are transient, not the contrast of the completed page/dialog.
  await page.mouse.move(0, 0);
  await expect(page.getByRole('tooltip')).toHaveCount(0);
  for (const dialog of await page.getByRole('dialog').all()) {
    await expect(dialog).not.toHaveClass(
      /ant-zoom-(?:enter|appear)(?:-active|-prepare)?(?:\s|$)/,
    );
    await expect(dialog).toHaveCSS('opacity', '1');
  }
  const result = await new AxeBuilder({ page })
    .withTags([
      'wcag2a',
      'wcag2aa',
      'wcag21a',
      'wcag21aa',
      'wcag22aa',
      'best-practice',
    ])
    .analyze();
  // Sanitized diagnostics only: never attach axe's raw HTML or page URL.
  const summary = (items: typeof result.violations) =>
    items.map((item) => ({
      rule: item.id,
      impact: item.impact,
      targets: item.nodes.map((node) => node.target),
    }));
  const path = info.outputPath(name.replace(/[^a-z0-9-]/gi, '-') + '-axe.json');
  await writeFile(
    path,
    JSON.stringify({
      violations: summary(result.violations),
      incomplete: summary(result.incomplete),
    }),
  );
  await info.attach(`${name}-axe.json`, {
    path,
    contentType: 'application/json',
  });
  expect(summary(result.violations), `Accessibility: ${name}`).toEqual([]);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
    `Page overflow: ${name}`,
  ).toBe(true);
}
async function navigate(page: Page, name: 'Users' | 'Audit log') {
  const menu = page.getByRole('button', { name: 'Open navigation' });
  if (await menu.isVisible()) await menu.click();
  await page.getByRole('link', { name, exact: true }).click();
  await expect(
    page.getByRole('heading', { name, exact: true, level: 1 }),
  ).toBeVisible();
  await expect(page.getByRole('dialog')).toHaveCount(0);
}
async function openReader(page: Page) {
  await page.goto('/admin/users?search=' + encodeURIComponent(reader));
  await page.getByRole('link', { name: `View details for ${reader}` }).click();
  await expect(
    page.getByRole('region', { name: 'Account information' }),
  ).toBeVisible();
  await expect(
    page
      .getByRole('region', { name: 'User sessions' })
      .getByText('Loading sessions…'),
  ).toHaveCount(0);
}

for (const width of [360, 768, 1280]) {
  test(`all administrative pages and confirmation dialogs are accessible at ${width}px`, async ({
    page,
  }, info) => {
    // This scenario deliberately audits nine loaded page/dialog states.
    test.setTimeout(90_000);
    await page.setViewportSize({ width, height: 900 });
    await page.goto('/admin/login');
    await expect(
      page.getByRole('button', { name: 'Sign in', exact: true }),
    ).toBeVisible();
    await audit(page, info, 'login');
    await signIn(page);
    await expect(
      page.getByRole('heading', { name: 'Dashboard', exact: true }),
    ).toBeFocused();
    await audit(page, info, 'dashboard');
    await navigate(page, 'Users');
    await expect(page.getByRole('table')).toBeVisible();
    await audit(page, info, 'users');
    await page
      .getByRole('button', { name: 'Invite user', exact: true })
      .click();
    const invite = page.getByRole('dialog', {
      name: 'Invite user',
      exact: true,
    });
    await expect(invite.getByLabel('Email', { exact: true })).toBeFocused();
    await audit(page, info, 'invitation-dialog');
    await page.keyboard.press('Escape');
    await expect(invite).toBeHidden();
    await expect(
      page.getByRole('button', { name: 'Invite user', exact: true }),
    ).toBeFocused();
    await openReader(page);
    for (const name of ['Account information', 'Recent security activity']) {
      const heading = page.getByRole('heading', { name, exact: true });
      expect(
        await heading.evaluate(
          (element) => element.scrollWidth <= element.clientWidth,
        ),
        'Section headings must wrap instead of clipping.',
      ).toBe(true);
    }
    await audit(page, info, 'user-details');
    for (const [trigger, title] of [
      ['Disable account', 'Disable account?'],
      ['Send password reset link', 'Send password reset link?'],
      ['Revoke all sessions', 'Revoke all sessions?'],
    ]) {
      await page.getByRole('button', { name: trigger, exact: true }).click();
      const dialog = page.getByRole('dialog', { name: title, exact: true });
      await expect(
        dialog.getByRole('button', { name: 'Cancel', exact: true }),
      ).toBeFocused();
      await audit(page, info, title ?? 'confirmation');
      await page.keyboard.press('Escape');
      await expect(dialog).toBeHidden();
      await expect(
        page.getByRole('button', { name: trigger, exact: true }),
      ).toBeFocused();
    }
    await page.screenshot({
      path: info.outputPath('user-details.png'),
      fullPage: true,
      animations: 'disabled',
    });
    await navigate(page, 'Audit log');
    await expect(
      page.getByRole('region', {
        name: 'Audit table · scroll horizontally for all columns',
      }),
    ).toBeVisible();
    await audit(page, info, 'audit-log');
  });

  test(`public forms and link states are accessible at ${width}px`, async ({
    page,
  }, info) => {
    test.setTimeout(60_000);
    await page.setViewportSize({ width, height: 900 });
    for (const flow of ['activate', 'reset-password']) {
      await page.goto(`/account/${flow}`);
      await expect(
        page.getByRole('heading', { name: 'Link required' }),
      ).toBeFocused();
      await audit(page, info, flow + '-missing');
      await page.goto(`/account/${flow}#token=${'K'.repeat(43)}`);
      await expect(
        page.getByLabel('New password', { exact: true }),
      ).toBeVisible();
      await expect.poll(() => new URL(page.url()).hash === '').toBe(true);
      await audit(page, info, flow + '-form');
      if (flow === 'reset-password')
        await page.screenshot({
          path: info.outputPath('public-reset.png'),
          fullPage: true,
          animations: 'disabled',
        });
    }
  });
}

test('ReadOnly has no write controls across users/details/sessions and can read audit', async ({
  page,
}, info) => {
  test.setTimeout(60_000);
  await signIn(page, true);
  await navigate(page, 'Users');
  await expect(
    page.getByRole('button', { name: 'Invite user', exact: true }),
  ).toHaveCount(0);
  await openReader(page);
  await expect(
    page.getByRole('button', {
      name: /Disable account|Enable account|Resend invitation|Send password reset link|Revoke/,
    }),
  ).toHaveCount(0);
  await audit(page, info, 'readonly-details');
  await navigate(page, 'Audit log');
  await expect(
    page.getByRole('region', {
      name: 'Audit table · scroll horizontally for all columns',
    }),
  ).toBeVisible();
});

test('real CSRF rejection stays inline and retries only after explicit resubmission', async ({
  page,
}) => {
  await signIn(page);
  await navigate(page, 'Users');
  await page.getByRole('button', { name: 'Invite user', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Invite user', exact: true });
  await dialog
    .getByLabel('Email', { exact: true })
    .fill(`zz-csrf-${crypto.randomUUID()}@identity.example`);
  let posts = 0;
  await page.route('**/api/v1/admin/users/invitations', async (route) => {
    posts++;
    const headers = { ...route.request().headers() };
    delete headers['x-csrf-token'];
    await route.continue({ headers });
  });
  const rejected = page.waitForResponse(
    (res) =>
      res.url().endsWith('/users/invitations') &&
      res.request().method() === 'POST',
  );
  await dialog.getByRole('button', { name: 'Send invitation' }).click();
  expect((await rejected).status()).toBe(400);
  await expect(dialog.getByRole('alert')).toContainText(
    'Your security check expired.',
  );
  expect(posts).toBe(1);
  await page.unroute('**/api/v1/admin/users/invitations');
  const accepted = page.waitForResponse(
    (res) =>
      res.url().endsWith('/users/invitations') &&
      res.request().method() === 'POST',
  );
  await dialog.getByRole('button', { name: 'Send invitation' }).click();
  expect((await accepted).status()).toBe(202);
  await expect(dialog).toBeHidden();
});

test('every protected admin route rejects anonymous access without exposing business controls', async ({
  page,
}) => {
  for (const path of [
    '/admin',
    '/admin/users',
    '/admin/users/00000000-0000-0000-0000-000000000001',
    '/admin/audit',
    '/admin/unknown',
  ]) {
    await page.goto(path);
    await expect(page).toHaveURL(/\/admin\/login$/);
    await expect(
      page.getByRole('button', { name: 'Sign in', exact: true }),
    ).toBeVisible();
    await expect(page.getByRole('navigation')).toHaveCount(0);
  }
});

test('session-check 401 removes the protected view without a full page reload', async ({
  page,
}) => {
  await signIn(page);
  await page.route('**/api/v1/admin/auth/me', (route) =>
    route.fulfill({
      status: 401,
      contentType: 'application/problem+json',
      body: JSON.stringify({ code: 'unauthorized' }),
    }),
  );
  await page.evaluate(() =>
    window.dispatchEvent(new Event('visibilitychange')),
  );
  await expect(
    page.getByRole('button', { name: 'Sign in', exact: true }),
  ).toBeVisible();
  await expect(page.getByRole('navigation')).toHaveCount(0);
  await expect(
    page.getByRole('heading', { name: 'Dashboard', exact: true }),
  ).toHaveCount(0);
});

test('status transitions move keyboard focus to the replacement action', async ({
  page,
}) => {
  await signIn(page);
  await navigate(page, 'Users');
  const email = `zz-focus-${crypto.randomUUID()}@identity.example`;
  await page.getByRole('button', { name: 'Invite user', exact: true }).click();
  let dialog = page.getByRole('dialog', { name: 'Invite user', exact: true });
  await dialog.getByLabel('Email', { exact: true }).fill(email);
  await dialog.getByRole('button', { name: 'Send invitation' }).click();
  await expect(dialog).toBeHidden();
  await page.getByLabel('Search users', { exact: true }).fill(email);
  await page.getByRole('link', { name: `View details for ${email}` }).click();
  await page
    .getByRole('button', { name: 'Disable account', exact: true })
    .click();
  dialog = page.getByRole('dialog', { name: 'Disable account?', exact: true });
  await dialog
    .getByLabel('Reason', { exact: true })
    .fill('Disposable keyboard focus check');
  await dialog
    .getByRole('button', { name: 'Confirm disable', exact: true })
    .click();
  await expect(
    page.getByRole('button', { name: 'Enable account', exact: true }),
  ).toBeFocused();
  await page.keyboard.press('Enter');
  dialog = page.getByRole('dialog', { name: 'Enable account?', exact: true });
  await dialog
    .getByRole('button', { name: 'Confirm enable', exact: true })
    .click();
  await expect(
    page.getByRole('button', { name: 'Disable account', exact: true }),
  ).toBeFocused();
});

test('keyboard-only sign-in, navigation and invitation cancellation preserve focus', async ({
  page,
}) => {
  const password = process.env.IDENTITY_E2E_PASSWORD;
  if (!password) throw new Error('Disposable credentials are required.');
  await page.goto('/admin/login');
  const email = page.getByLabel('Email', { exact: true });
  await expect(email).toBeVisible();
  await tabTo(page, email);
  await page.keyboard.insertText('admin@identity.example');
  await page.keyboard.press('Tab');
  try {
    await page.keyboard.insertText(password);
  } catch {
    throw new Error('Could not enter disposable credentials.');
  }
  await tabTo(page, page.getByRole('button', { name: 'Sign in', exact: true }));
  await page.keyboard.press('Enter');
  await expect(
    page.getByRole('heading', { name: 'Dashboard', exact: true }),
  ).toBeFocused();
  await tabTo(page, page.getByRole('link', { name: 'Users', exact: true }));
  await page.keyboard.press('Enter');
  await expect(
    page.getByRole('heading', { name: 'Users', exact: true }),
  ).toBeFocused();
  const invite = page.getByRole('button', { name: 'Invite user', exact: true });
  await tabTo(page, invite);
  await page.keyboard.press('Enter');
  const dialog = page.getByRole('dialog', { name: 'Invite user', exact: true });
  await expect(dialog.getByLabel('Email', { exact: true })).toBeFocused();
  await tabTo(
    page,
    dialog.getByRole('button', { name: 'Cancel', exact: true }),
  );
  await page.keyboard.press('Enter');
  await expect(dialog).toBeHidden();
  await expect(invite).toBeFocused();
  await tabTo(page, page.getByRole('link', { name: 'Audit log', exact: true }));
  await page.keyboard.press('Enter');
  await expect(
    page.getByRole('heading', { name: 'Audit log', exact: true }),
  ).toBeFocused();
});
