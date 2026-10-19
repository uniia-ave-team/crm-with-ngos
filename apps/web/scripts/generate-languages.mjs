// Складає список мов застосунку з наявних файлів перекладів `src/assets/i18n/*.json`:
//   код мови — імʼя файлу без розширення (`uk.json` → `uk`);
//   назва — значення ключа `name` у файлі; якщо його нема чи воно порожнє —
//   сам код мови (запасний варіант).
// Результат — `src/app/core/i18n/languages.generated.ts` (в git не комітиться,
// відтворюється перед стартом, збіркою, тестами й лінтом).
import { mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { basename, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const i18nDir = join(root, 'src', 'assets', 'i18n');
const output = join(root, 'src', 'app', 'core', 'i18n', 'languages.generated.ts');

/** Список мов за файлами в каталозі: `[{ code, name }]`, за кодом. */
export function collectLanguages(directory = i18nDir) {
  return readdirSync(directory)
    .filter((file) => file.endsWith('.json'))
    .map((file) => {
      const code = basename(file, '.json');
      let translations;
      try {
        translations = JSON.parse(readFileSync(join(directory, file), 'utf8'));
      } catch (error) {
        throw new Error(`Файл мови ${file} не є коректним JSON: ${error.message}`);
      }
      const name = typeof translations?.name === 'string' ? translations.name.trim() : '';
      return { code, name: name || code };
    })
    .sort((a, b) => a.code.localeCompare(b.code));
}

export function render(languages) {
  const items = languages.map(({ code, name }) => `  { code: ${JSON.stringify(code)}, name: ${JSON.stringify(name)} },`);
  return `// Згенеровано scripts/generate-languages.mjs з src/assets/i18n/*.json — не редагувати.

/** Мова застосунку: код (імʼя файлу перекладів) і назва (ключ \`name\` у файлі; без нього — код). */
export interface LanguageOption {
  readonly code: string;
  readonly name: string;
}

/** Усі мови, для яких є файл перекладів. */
export const LANGUAGES: readonly LanguageOption[] = [
${items.join('\n')}
];
`;
}

// Запуск напряму (`node scripts/generate-languages.mjs`): записує файл.
if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const languages = collectLanguages();
  mkdirSync(dirname(output), { recursive: true });
  writeFileSync(output, render(languages));
  console.log(`[generate-languages] ${languages.map((l) => `${l.code} (${l.name})`).join(', ')}`);
}
