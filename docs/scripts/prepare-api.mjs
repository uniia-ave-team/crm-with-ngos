// Copies the OpenAPI contract and the Scalar bundle next to public/api/index.html.
// Both are build inputs, not sources, and are ignored by git.
import { copyFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const docsDir = join(dirname(fileURLToPath(import.meta.url)), '..');
const target = join(docsDir, 'public', 'api');

mkdirSync(target, { recursive: true });
copyFileSync(join(docsDir, '..', 'packages', 'api-contract', 'openapi.json'), join(target, 'openapi.json'));
copyFileSync(
  join(docsDir, 'node_modules', '@scalar', 'api-reference', 'dist', 'browser', 'standalone.js'),
  join(target, 'scalar.js'),
);
