import { test } from './fixture';
import { expect } from '@playwright/test';
import { signIn } from './authFixture';

for (const width of [360, 768, 1280]) {
  test(`standard Ant Design geometry and accessible theme at ${width}px`, async ({
    page,
  }, info) => {
    await page.setViewportSize({ width, height: 900 });
    await signIn(page);
    await expect(
      page.getByRole('region', { name: 'Total users', exact: true }),
    ).toBeVisible();
    await page.screenshot({
      animations: 'disabled',
      path: info.outputPath('dashboard.png'),
      fullPage: true,
    });
    await page.goto('/admin/users');
    await expect(page.getByRole('table')).toBeVisible();
    const invite = page.getByRole('button', {
      name: 'Invite user',
      exact: true,
    });
    // Verify actual browser styles, not only presence of Ant component markup.
    await expect(invite).toHaveCSS('background-color', 'rgb(9, 88, 217)');
    await expect(invite).toHaveCSS('height', '32px');
    await expect(invite).toHaveCSS('border-radius', '6px');
    await expect(
      page.getByRole('textbox', { name: 'Search users' }).locator('..'),
    ).toHaveCSS('height', '32px');
    const longEmail = page.getByRole('cell', {
      name: /^zz-invited-.*@identity.example$/,
    });
    await expect(longEmail).toHaveCSS('white-space', 'nowrap');
    await expect(longEmail).toHaveCSS('text-overflow', 'ellipsis');
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
    await page.screenshot({
      animations: 'disabled',
      path: info.outputPath('users.png'),
      fullPage: true,
    });
    await invite.click();
    const dialog = page.getByRole('dialog', {
      name: 'Invite user',
      exact: true,
    });
    await expect(dialog).toBeVisible();
    // Visibility/focus can precede rc-motion's enter-active frame. Wait for the
    // completed dialog, so a screenshot cannot show only its already-open mask.
    await expect(dialog).not.toHaveClass(
      /ant-zoom-(?:enter|appear)(?:-active|-prepare)?(?:\s|$)/,
    );
    await expect(dialog).toHaveCSS('opacity', '1');
    const send = dialog.getByRole('button', {
      name: 'Send invitation',
      exact: true,
    });
    await expect(send).toHaveCSS('background-color', 'rgb(9, 88, 217)');
    await expect(dialog.getByLabel('Email', { exact: true })).toHaveCSS(
      'height',
      '32px',
    );
    expect(
      await send.evaluate((element) =>
        Boolean(element.closest('.ant-modal-footer')),
      ),
    ).toBe(true);
    await expect(dialog.getByLabel('Email', { exact: true })).toBeFocused();
    await page.screenshot({
      animations: 'disabled',
      path: info.outputPath('invite-user.png'),
      fullPage: true,
    });
    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
    await expect(invite).toBeFocused();
  });
}
