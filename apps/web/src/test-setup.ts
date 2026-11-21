import { registerIcons } from './app/core/icons';

// Іконки застосунку: у браузері їх реєструє `main.ts`. Без цього `ion-icon` у тестах шукає
// файл іконки за адресою, якої в jsdom нема, і засмічує журнал помилками «Invalid URL».
registerIcons();

// Активний акаунт і мова живуть у `sessionStorage` вкладки. Кожен тест починає як нова вкладка,
// інакше вибір попереднього тесту перейшов би в наступний.
beforeEach(() => sessionStorage.clear());

// Оверлеї Ionic (спливаючі й модальні вікна), що лишилися в документі від попереднього тесту,
// прибираємо на початку наступного: інакше він отримає чуже вікно, а його пастка фокуса
// перехоплюватиме фокус (так часом падав тест фокуса `side-panel`). Саме на початку, а не одразу
// після тесту: тоді Angular уже прибрав компоненти попереднього тесту разом з їхніми вікнами, а
// вікно, яке ще відкривається, втратило б свій компонент посеред анімації (Ionic кидає помилку
// «framework delegate is missing»).
beforeEach(() => {
  document.querySelectorAll('ion-popover, ion-modal, ion-backdrop').forEach((overlay) => overlay.remove());
});

// Polyfills for running unit tests under jsdom (the default Vitest environment).
// Ionic components such as ion-menu and ion-split-pane query `window.matchMedia`,
// which jsdom does not implement.
if (!window.matchMedia) {
  window.matchMedia = (query: string): MediaQueryList =>
    ({
      matches: false,
      media: query,
      onchange: null,
      addListener: () => undefined,
      removeListener: () => undefined,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }) as MediaQueryList;
}
