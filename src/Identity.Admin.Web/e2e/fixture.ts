import { test as base, expect, type Page } from '@playwright/test';

export function protectPage(
  page: Page,
  initialSecrets: readonly string[] = [],
) {
  const secrets = new Set(initialSecrets.filter(Boolean));
  const diagnostics: string[] = [];
  const requests: string[] = [];
  const onConsole = (message: { text(): string }) =>
    diagnostics.push(message.text());
  const onRequest = (request: { url(): string; postData(): string | null }) => {
    requests.push(request.url());
    try {
      const body: unknown = JSON.parse(request.postData() ?? 'null');
      if (typeof body === 'object' && body !== null)
        for (const [key, value] of Object.entries(body))
          if (
            /^(password|currentPassword|newPassword|token|refreshToken)$/i.test(
              key,
            ) &&
            typeof value === 'string' &&
            value
          )
            secrets.add(value);
    } catch {
      /* Non-JSON requests contain no account JSON credentials. */
    }
  };
  page.on('console', onConsole);
  page.on('request', onRequest);
  const screenshot = page.screenshot.bind(page);
  page.screenshot = async (options) => {
    const empty = await page
      .locator(
        'input[type=password],input[autocomplete=current-password],input[autocomplete=new-password]',
      )
      .evaluateAll((inputs) =>
        inputs.every(
          (input) => input instanceof HTMLInputElement && !input.value,
        ),
      );
    expect(empty, 'Screenshots must contain empty password controls.').toBe(
      true,
    );
    expect(
      /[?#](?:[^#]*[&])?(?:token|password|access_token|refresh_token)=/i.test(
        page.url(),
      ),
      'Screenshot URL must not carry credentials.',
    ).toBe(false);
    return screenshot(options);
  };
  return {
    addSecret: (secret: string) => {
      if (secret) secrets.add(secret);
    },
    async verify() {
      expect(
        [...diagnostics, ...requests, page.url()].some((text) =>
          [...secrets].some((secret) => text.includes(secret)),
        ),
        'Credentials must not appear in console or request/page URLs.',
      ).toBe(false);
      if (!page.isClosed() && new URL(page.url()).protocol === 'https:') {
        expect(
          await page.evaluate(
            () => localStorage.length + sessionStorage.length,
          ),
          'Browser storage must stay empty.',
        ).toBe(0);
        expect(
          await page.evaluate(() =>
            document.cookie.includes('__Host-Identity.Admin='),
          ),
          'Admin ticket must be HttpOnly.',
        ).toBe(false);
      }
    },
    async dispose() {
      page.off('console', onConsole);
      page.off('request', onRequest);
      page.screenshot = screenshot;
      // Playwright failure snapshots must not retain field values, even after
      // the password visibility toggle changes the input type to text.
      if (!page.isClosed())
        await page
          .evaluate(() => {
            document
              .querySelectorAll<HTMLInputElement>(
                'input[type=password],input[autocomplete=current-password],input[autocomplete=new-password]',
              )
              .forEach((input) => {
                input.value = '';
              });
            if (/token=/i.test(location.hash))
              history.replaceState(
                history.state,
                '',
                location.pathname + location.search,
              );
          })
          .catch(() => undefined);
    },
  };
}

export const test = base.extend<{ privacy: void }>({
  privacy: [
    async ({ context }, runTest) => {
      const guards = new Map<Page, ReturnType<typeof protectPage>>();
      const observe = (page: Page) =>
        guards.set(
          page,
          protectPage(page, [process.env.IDENTITY_E2E_PASSWORD ?? '']),
        );
      context.pages().forEach(observe);
      context.on('page', observe);
      try {
        await runTest();
        for (const guard of guards.values()) await guard.verify();
        const cookies = await context.cookies();
        expect(
          cookies
            .filter((cookie) => cookie.name === '__Host-Identity.Admin')
            .every(
              (cookie) =>
                cookie.httpOnly &&
                cookie.secure &&
                cookie.sameSite === 'Strict',
            ),
          'Admin cookie attributes must remain secure.',
        ).toBe(true);
      } finally {
        context.off('page', observe);
        for (const guard of guards.values()) await guard.dispose();
      }
    },
    { auto: true },
  ],
});
