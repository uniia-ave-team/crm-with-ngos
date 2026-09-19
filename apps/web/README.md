# Вебклієнт

Клієнт на Ionic Angular. Каталог самодостатній: `package.json` і
`package-lock.json` розташовуються тут, а не в корені репозиторію. Спільного
workspace з `apps/api` немає.

Наразі це порожній проєкт, створений зі стартового шаблону Ionic (sidebar).
Функціональності CRM у ньому ще немає. Пакет називається `Yavir`.

## Стек

| Частина          | Версія / інструмент                                   |
| ---------------- | ----------------------------------------------------- |
| Фреймворк        | Angular 22, NgModule-архітектура (не standalone)      |
| UI               | Ionic Framework 9 (`@ionic/angular/lazy`), Ionicons 8 |
| Мова             | TypeScript 6                                          |
| Збірка           | Angular CLI 22, builder `@angular/build:application`  |
| Тести            | Vitest 4 + jsdom через `ng test`                      |
| Лінтер           | ESLint 10, angular-eslint, typescript-eslint          |
| Менеджер пакетів | npm                                                   |

Клієнт API генерується з `packages/api-contract/openapi.json`.

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

- каркас застосунку: `AppModule`, маршрутизація, `AppComponent` з бічним меню;
- демонстраційна сторінка `folder` зі стартового шаблону (Inbox, Outbox тощо),
  яку слід замінити на реальні сторінки;
- налаштування ESLint, TypeScript і Vitest;
- тема Ionic у `src/theme/variables.scss`;
- `environment.ts` / `environment.prod.ts` з єдиним прапорцем `production`.

Ще не зроблено:

- `proxy.conf.json` і `proxyConfig` в `angular.json` (без цього `just dev-web`
  не проксує `/api` на `http://localhost:5080`);
- `Dockerfile` і `nginx.conf` (потрібні для `just build`);
- клієнт API з OpenAPI та каталог `src/app/api/generated`;
- локалізація: `assets/i18n/uk.json`, тексти шаблону поки англійською;
- скрипт `format` (на нього посилається `justfile`);
- каталоги `core`, `shared`, `features`.

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
  global.scss
  test-setup.ts                налаштування Vitest
  theme/variables.scss         змінні теми Ionic
  environments/
  assets/
  app/
    app.module.ts
    app-routing.module.ts
    app.component.*            бічне меню
    folder/                    демонстраційна сторінка шаблону
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

**Локалізація.** Первинна мова інтерфейсу — українська. Тексти зберігаються
окремо від компонентів з першого коміту.

## Мобільні застосунки

Ionic дає можливість зібрати клієнт як мобільний застосунок через Capacitor.
Пакети `@capacitor/*` уже є в `package.json` (частина шаблону), але
`capacitor.config` і нативні проєкти відсутні. Рішення про мобільний застосунок
не ухвалене. Його наслідок для архітектури описаний у
[ADR 0001](../../docs/architecture/adr/0001-monorepo-and-lockstep-versioning.md#мобільний-клієнт):
мобільний застосунок стає другим споживачем API з власним циклом випуску, що
вимагає політики сумісності API, якої зараз немає.
