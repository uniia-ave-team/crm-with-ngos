# Вебклієнт

Клієнт на Ionic Angular. Каталог самодостатній: `package.json` і
`package-lock.json` розташовуються тут, а не в корені репозиторію. Спільного
workspace з `apps/api` немає. Пакет називається `Yavir`.

Застосунок уже має робочу оболонку (бокове меню, шість сторінок, локалізація,
сторінка входу), але без підключення до `apps/api`: сторінка входу показує
інтерфейс, решта сторінок поки порожні заглушки без вмісту.

## Стек

| Частина          | Версія / інструмент                                              |
|------------------|------------------------------------------------------------------|
| Фреймворк        | Angular 22, NgModule-архітектура (не standalone)                 |
| UI               | Ionic Framework 9 (`@ionic/angular/lazy`), Ionicons 8            |
| Локалізація      | `@ngx-translate/core` 18 (standalone API, без `TranslateModule`) |
| Шрифт            | e-Ukraine (Regular/Light/Medium/Bold), локально в `assets/fonts` |
| Мова             | TypeScript 6                                                     |
| Збірка           | Angular CLI 22, builder `@angular/build:application`             |
| Тести            | Vitest 4 + jsdom через `ng test`                                 |
| Лінтер           | ESLint 10, angular-eslint, typescript-eslint                     |
| Менеджер пакетів | npm                                                              |

## Вимоги

Node.js **22.22.3 або новіший** (також підходять 24.15+ і 26+).

## Команди

Усі команди виконуються в `apps/web`.

```bash
npm install                        # встановити залежності
npm start                          # ng serve, http://localhost:4200
npm run build                      # production-збірка у www/
npm run watch                      # збірка з перезбиранням (development)
npm test                           # Vitest у режимі спостереження
npx ng test --configuration ci     # Vitest, один прогін (для CI)
npm run lint                       # ESLint для src/**/*.ts і src/**/*.html
```

`ng build` кладе результат у `www/`, а не в `dist/`. Саме звідти Dockerfile
має копіювати статику для nginx. Каталог `www/` ігнорується git.

Бюджети production-збірки: початковий бандл — попередження від 2 МБ, помилка
від 5 МБ; стилі компонента — 2 і 4 КБ.

## Поточний стан

Створено:

- **Каркас і навігація.** `AppComponent` — оболонка застосунку: бокова панель
  (`core/layout/primary-nav`, десктопна колонка / мобільні верхня й нижня
  панелі) і друга контекстна панель (`core/layout/secondary-panel`) для
  сторінок «База знань» і «Планер». Сторінки без бічного меню взагалі
  (наразі — лише `/login`) позначаються `data: { layout: 'none' }` у
  маршруті й читаються через `LayoutService.hideShell`.
- **Шість сторінок:** `calendar`, `planner`, `knowledge-base`, `colleagues`,
  `settings` (порожні заглушки з перекладеним заголовком) і `login` —
  єдина сторінка з реальним вмістом: форма «Крок 1: введіть адресу серверу»
  без бічного меню. Кнопка «Продовжити» поки без обробника.
- **Локалізація.** Реалізовано завантаження рядків локалізації для бічного
  меню й заголовків сторінок; **усі текстові рядки в усіх чотирьох мовах —
  тимчасові плейсхолдери** та будуть замінені. `@ngx-translate/core` 18
  (сучасний standalone API, без `TranslateModule` —
  `provideTranslateService`/`provideTranslateHttpLoader` і стандалонний
  `TranslatePipe`). Чотири мови — українська, англійська, кримськотатарська,
  білоруська (`src/assets/i18n/*.json`). Мова визначається автоматично з мови
  пристрою при першому відкритті (`LanguageService`, `core/i18n`),
  зберігається в `localStorage` й синхронізується між вкладками. Для
  кримськотатарської «запасна» мова — українська, для решти — англійська.
- **`ConnectionService`** (`core/connection`) — заготовка для зберігання
  адреси сервера (`localStorage`, ключ `yavir.serverAddress`). Ще не
  підключена до форми входу і не виконує ні вхід, ні автопошук сервера.
- **Шрифт e-Ukraine** — чотири ваги (Regular, Light, Medium, Bold),
  `assets/fonts/*.otf`, ліцензія CC BY 4.0 (джерело — дзеркало офіційного
  шрифту Мінцифри, `assets/fonts/LICENSE.txt`).
- **Мобільне лого** — окремі SVG для кожної мови (`assets/logo_mobile_*.svg`):
  кириличний варіант для української, латинський — для англійської й
  кримськотатарської, окремий — для білоруської.
- Кольорові токени світлої/темної теми та `ion-toolbar` (`src/theme/`).
- `environment.ts` / `environment.prod.ts` з єдиним прапорцем `production`.

Ще не зроблено:

- `proxy.conf.json` і `proxyConfig` в `angular.json` (без цього `just dev-web`
  не проксує `/api` на `http://localhost:5080`);
- `Dockerfile` і `nginx.conf` (потрібні для `just build`);
- UI самих сторінок — `calendar`, `planner`, `knowledge-base`, `colleagues`
  поки порожні заглушки;
- сторінка `settings` — теж порожня заглушка;
- підтягування даних з API — усе, що є, працює лише візуально, без реальних
  даних;
- реальний вхід: `ConnectionService` не підключений до форми, кнопка
  «Продовжити» нічого не робить, автопошуку сервера немає;
- переклад вмісту сторінок (лише меню й заголовки перекладені);
- перемикач мови в інтерфейсі (наразі тільки автовизначення);
- скрипт `format` (на нього посилається `justfile`);
- каталог `shared/` (спільні компоненти поза `core/` і сторінками поки не
  виділялись).

## Структура

```
.gitignore
angular.json                   єдиний проєкт `app`, вихід збірки — www/
ionic.config.json
package.json
eslint.config.js
tsconfig.json                  також tsconfig.app.json, tsconfig.spec.json
src/
  main.ts                      завантажує AppModule
  index.html
  global.scss                  імпорти Ionic, шрифтів, токенів, ion-toolbar
  test-setup.ts                налаштування Vitest
  theme/
    variables.scss             змінні теми Ionic
    nav-tokens.scss            кольорові токени бічного меню й тла сторінок
    fonts.scss                 @font-face e-Ukraine і токени --font-*
  environments/
  assets/
    fonts/                     e-Ukraine (Regular/Light/Medium/Bold) + LICENSE
    i18n/                      uk.json, en.json, crh.json, be.json
    logo*.svg, logo_mobile_*.svg, zoria*.svg
  app/
    app.module.ts
    app-routing.module.ts
    app.component.*            оболонка: рейл + друга панель
    core/
      layout/                  LayoutService, PrimaryNavComponent,
                                SecondaryPanelComponent
      i18n/                    LanguageService
      connection/               ConnectionService (заготовка)
    calendar/
    planner/
    knowledge-base/
    colleagues/
    settings/
    login/
```

## Обмеження, що випливають зі способу постачання

**Статична збірка.** Клієнт — односторінковий застосунок; результат `ng build`
віддає nginx. Образ не містить Node.js. Це знімає потребу в серверному рантаймі
на боці адміністратора інсталяції.

**Порт.** Контейнер слухає порт 8080 (non-root nginx); саме туди проксує Caddy
згідно з конфігурацією в `deploy/docker-compose.yml`.

**Конфігурація під час виконання.** Один образ працює на всіх інсталяціях, тому
нічого специфічного для середовища не вбудовується у збірку, зокрема в
`environments/`. Звернення до API йдуть відносним шляхом `/api`; Caddy
розміщує клієнт і API на одному origin, тож CORS не потрібен, а сесія живе в
httpOnly-куці.

**Без серверної логіки в клієнті.** Автентифікація, валідація та бізнес-правила
реалізовані в `apps/api`. Клієнт не проксує та не агрегує виклики API.

**Локалізація.** Інтерфейс підтримує українську, англійську, кримськотатарську
й білоруську мови; мова визначається автоматично з мови пристрою при першому
відкритті. Тексти зберігаються окремо від компонентів (`assets/i18n/*.json`)
з першого коміту.

## Мобільні застосунки

Ionic дає можливість зібрати клієнт як мобільний застосунок через Capacitor.
Пакети `@capacitor/*` уже є в `package.json` (частина шаблону), але
`capacitor.config` і нативні проєкти відсутні. Рішення про мобільний застосунок
не ухвалене. Його наслідок для архітектури описаний у
[ADR 0001](../../docs/architecture/adr/0001-monorepo-and-lockstep-versioning.md#мобільний-клієнт):
мобільний застосунок стає другим споживачем API з власним циклом випуску, що
вимагає політики сумісності API, якої зараз немає.
