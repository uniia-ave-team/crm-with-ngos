// Перевіряє, що кожна іконка, назву якої пишуть у шаблонах і коді, зареєстрована в
// src/app/core/icons.ts. Незареєстрована іконка не дає помилки збірки чи тесту: ion-icon просто
// лишає порожнє місце. Запускається перед лінтом (npm-хук `prelint`), тож забута іконка ламає CI.
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(fileURLToPath(new URL('.', import.meta.url)), '..');
const appDir = join(root, 'src', 'app');
const registryFile = join(appDir, 'core', 'icons.ts');

/** Усі файли каталогу з таким розширенням (без згенерованого клієнта API й тестів). */
function files(dir, extension) {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) {
      return path.endsWith(join('api', 'generated')) ? [] : files(path, extension);
    }
    return path.endsWith(extension) && !path.endsWith('.spec.ts') ? [path] : [];
  });
}

/**
 * Рядкові літерали у виразі шаблону, які є назвами іконок: `a ? 'x' : 'y'` → x, y. Літерал, з
 * яким щось порівнюють (`direction === 'asc'`), — не назва іконки.
 */
const literals = (expression) =>
  [...expression.matchAll(/([=!]==?\s*)?'([a-z][a-z-]*)'/g)].filter((match) => !match[1]).map((match) => match[2]);

/** Назва іконки → файли, де її вжито. */
const used = new Map();
const use = (name, file) => used.set(name, [...(used.get(name) ?? []), relative(root, file)]);

for (const file of files(appDir, '.html')) {
  const html = readFileSync(file, 'utf8');
  for (const [tag] of html.matchAll(/<ion-icon\b[^>]*>/gs)) {
    const fixed = tag.match(/(?<![[\w])name="([a-z-]+)"/);
    if (fixed) {
      use(fixed[1], file);
    }
    const bound = tag.match(/\[name\]="([^"]+)"/);
    if (bound) {
      literals(bound[1]).forEach((name) => use(name, file));
    }
  }
  // Іконка, передана компоненту входом `icon` (наприклад, плашці ролі).
  for (const [, bound, fixed] of html.matchAll(/\[icon\]="([^"]+)"|(?<![[\w-])icon="([a-z-]+)"/g)) {
    (bound ? literals(bound) : [fixed]).forEach((name) => use(name, file));
  }
}

for (const file of files(appDir, '.ts')) {
  if (file === registryFile) {
    continue;
  }
  const source = readFileSync(file, 'utf8');
  // Назви Ionicons щонайменше з трьох літер; коротші значення поля `icon` — не іконки
  // (наприклад, літера-позначка умови пароля).
  for (const [, name] of source.matchAll(/\bicon(?:Active|Name)?\??\s*[:=]\s*'([a-z][a-z-]{2,})'/g)) {
    use(name, file);
  }
}

const registered = new Set([...readFileSync(registryFile, 'utf8').matchAll(/^\s*'([a-z-]+)':/gm)].map((match) => match[1]));

const missing = [...used.keys()].filter((name) => !registered.has(name)).sort();
const unused = [...registered].filter((name) => !used.has(name)).sort();

if (unused.length > 0) {
  console.warn(`[check-icons] зареєстровані, але ніде не вжиті: ${unused.join(', ')}`);
}
if (missing.length > 0) {
  console.error('[check-icons] іконки, яких нема в src/app/core/icons.ts (вони не намалюються):');
  for (const name of missing) {
    console.error(`  ${name} — ${[...new Set(used.get(name))].join(', ')}`);
  }
  process.exit(1);
}
console.log(`[check-icons] ${used.size} іконок, усі зареєстровані`);
