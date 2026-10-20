import { expect } from '@playwright/test';
import { test } from './fixture';
import { signIn } from './authFixture';

test('TLS edge serves deep links with enforced CSP and correct cache policy', async ({
  page,
  request,
}) => {
  const response = await page.goto('/admin/users');
  expect(response?.status()).toBe(200);
  const headers = response!.headers();
  expect(headers['strict-transport-security']).toBe('max-age=31536000');
  expect(headers['referrer-policy']).toBe('no-referrer');
  expect(headers['x-content-type-options']).toBe('nosniff');
  expect(headers['cache-control']).toBe('no-store');
  expect(headers['content-security-policy']).toContain("script-src 'self'");
  const script = await page.locator('script[src]').first().getAttribute('src');
  const asset = await request.get(script!);
  expect(asset.status()).toBe(200);
  expect(asset.headers()['cache-control']).toBe(
    'public, max-age=31536000, immutable',
  );
  const missing = await request.get('/assets/missing-12345678.js');
  expect(missing.status()).toBe(404);
  expect(missing.headers()['cache-control']).toBe('no-store');
});

test('API misses and private operational routes never fall back to SPA HTML', async ({
  request,
}) => {
  const missing = await request.get('/api/v1/unknown');
  expect(missing.status()).toBe(404);
  expect(missing.headers()['content-type']).toContain(
    'application/problem+json',
  );
  for (const path of [
    '/health/ready',
    '/health/live',
    '/openapi/v1.json',
    '/index.html',
    '/unrelated',
  ]) {
    const response = await request.get(path);
    expect(response.status()).toBe(404);
    expect(await response.text()).not.toContain('<div id="root">');
  }
  const unknownHost = await request.get('/admin/login', {
    headers: { Host: 'untrusted.example' },
  });
  expect(unknownHost.status()).toBe(421);
});

test('proxy overwrites spoofed forwarding headers and preserves real 401 and CSRF errors', async ({
  request,
}) => {
  const unauthenticated = await request.get('/api/v1/admin/auth/me', {
    headers: {
      'X-Forwarded-Proto': 'http',
      'X-Forwarded-For': '10.8.0.1',
      'X-Forwarded-Host': 'untrusted.example',
    },
  });
  expect(unauthenticated.status()).toBe(401);
  expect(unauthenticated.headers()['cache-control']).toBe('no-store');
  expect(unauthenticated.headers()['content-security-policy']).toBe(
    "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'",
  );
  const csrf = await request.post('/api/v1/admin/auth/login', { data: {} });
  expect(csrf.status()).toBe(400);
  expect(((await csrf.json()) as { code: string }).code).toBe('csrf_invalid');
  const jwks = await request.get('/.well-known/jwks.json');
  expect(jwks.status()).toBe(200);
});

test('Ant Design runtime styles, chart, modal and notifications obey enforced CSP', async ({
  page,
}) => {
  await page.addInitScript(() => {
    const violations: string[] = [];
    Object.defineProperty(window, '__identityCspViolations', {
      value: violations,
    });
    document.addEventListener('securitypolicyviolation', (event) =>
      violations.push(event.effectiveDirective),
    );
  });
  await signIn(page);
  await expect(
    page.getByRole('group', { name: /Registrations/i }),
  ).toBeVisible();
  await page.getByRole('link', { name: 'Users', exact: true }).click();
  await page.getByRole('button', { name: 'Invite user', exact: true }).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await page
    .getByRole('dialog')
    .getByLabel('Email', { exact: true })
    .fill(`csp-${crypto.randomUUID()}@identity.example`);
  await page
    .getByRole('dialog')
    .getByRole('button', { name: 'Send invitation', exact: true })
    .click();
  await expect(
    page.getByText('Invitation accepted for processing.', { exact: true }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => Reflect.get(window, '__identityCspViolations') as string[],
    ),
  ).toEqual([]);
  // A harmless inline script must actually be blocked, rather than a report-only policy.
  await page.evaluate(() => {
    const script = document.createElement('script');
    script.textContent = 'window.__identityInlineScriptExecuted = true';
    document.head.append(script);
  });
  await expect
    .poll(() =>
      page.evaluate(() =>
        (Reflect.get(window, '__identityCspViolations') as string[]).includes(
          'script-src-elem',
        ),
      ),
    )
    .toBe(true);
  expect(
    await page.evaluate(() =>
      Boolean(Reflect.get(window, '__identityInlineScriptExecuted')),
    ),
  ).toBe(false);
});
