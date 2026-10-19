import { readFile, writeFile } from 'node:fs/promises';
import openapiTS, { astToString } from 'openapi-typescript';
import { format, resolveConfig } from 'prettier';
import { loadEnv } from 'vite';

try {
  const env = loadEnv('development', process.cwd(), 'IDENTITY_');
  const source = new URL(
    env.IDENTITY_OPENAPI_URL || 'http://127.0.0.1:5080/openapi/v1.json',
  );
  if (
    !['http:', 'https:'].includes(source.protocol) ||
    source.username ||
    source.password ||
    source.search ||
    source.hash
  ) {
    throw new Error('Invalid OpenAPI source.');
  }

  const response = await fetch(source, {
    credentials: 'omit',
    redirect: 'error',
    signal: AbortSignal.timeout(15_000),
  });
  if (!response.ok) throw new Error('OpenAPI is unavailable.');
  const schema = await response.json();
  // These success contracts must remain visible; never patch the schema locally.
  for (const path of [
    '/api/v1/admin/users/{userId}',
    '/api/v1/admin/users/{userId}/sessions',
  ]) {
    if (
      !schema.paths?.[path]?.get?.responses?.['200']?.content?.[
        'application/json'
      ]
    ) {
      throw new Error('An administrative success contract is missing.');
    }
  }

  const output = new URL('../src/api/schema.d.ts', import.meta.url);
  const generated = await format(
    '/* Generated from Identity.Api OpenAPI. Do not edit; run npm run api:generate. */\n' +
      astToString(await openapiTS(schema)),
    {
      ...(await resolveConfig(output)),
      parser: 'typescript',
    },
  );
  if (process.argv.includes('--check')) {
    if ((await readFile(output, 'utf8')) !== generated) {
      throw new Error('Generated types differ from the backend contract.');
    }
    console.log('OpenAPI types match Identity.Api.');
  } else {
    await writeFile(output, generated, 'utf8');
    console.log('Generated src/api/schema.d.ts from Identity.Api.');
  }
} catch {
  // Avoid logging remote response bodies, source URLs or environment values.
  console.error(
    'OpenAPI generation failed. Check the source, success contracts and generated types; no files were changed on fetch/validation failure.',
  );
  process.exitCode = 1;
}
