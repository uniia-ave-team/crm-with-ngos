# Ліцензії сторонніх іконок

Ця тека копіюється в збірку (`www/licenses`), тож ліцензії поширюються разом із застосунком.

| Набір | Пакет | Ліцензія | Де показуємо |
|---|---|---|---|
| Lucide | `lucide-static` | ISC; частина іконок — з Feather, MIT ([lucide/LICENSE](lucide/LICENSE)) | Safari |
| Material Symbols | `@material-symbols/svg-400` (Outlined, вага 400) | Apache 2.0 ([material-symbols/LICENSE](material-symbols/LICENSE)) | інші браузери |

- Файли `LICENSE` скопійовано з пакетів без змін: у них оригінальні згадки про авторські права.
- Кожен SVG Lucide зберігає свій коментар `@license` і в збірці.
- У пакеті Material Symbols файлу `NOTICE` нема.
- Зміни Material Symbols: під час реєстрації (`src/app/core/icons.ts`) у кореневий тег `<svg>`
  додається `fill="currentColor"`, щоб іконка брала колір тексту; кожна така іконка отримує на
  початку примітку про цю зміну (`MATERIAL_FILL_NOTICE`; Apache 2.0, п. 4b).
- Змінені копії іконок (`derive` у `src/app/core/icons.ts`): «видалити рядок» і «видалити стовпчик»
  — з Lucide `rows-2-plus-bottom` / `columns-2-plus-right` (прибрано вертикальну риску плюса) і з
  Material Symbols `add_row_below` / `add_column_right` (контур плюса замінено
  прямокутником-мінусом). Кожна змінена іконка містить примітку про зміну (коментар перед `<svg>`;
  Apache 2.0, п. 4b), у Lucide лишається її коментар `@license`. Оригінальні файли пакетів не
  змінюються.
