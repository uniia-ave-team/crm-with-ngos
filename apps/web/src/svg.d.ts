/** SVG з пакетів іконок імпортується текстом (`loader: { ".svg": "text" }` в `angular.json`). */
declare module '*.svg' {
  const content: string;
  export default content;
}
