/**
 * Унікальний ключ нового поля (стовпчика) таблиці чи шаблону: `column_N` (snake_case, як вимагає
 * сервер), N — від кількості полів + 1 і далі, доки ключ не вільний.
 */
export function uniqueFieldKey(fields: readonly { readonly key: string }[]): string {
  const taken = new Set(fields.map((field) => field.key));
  for (let number = fields.length + 1; ; number++) {
    const key = `column_${number}`;
    if (!taken.has(key)) {
      return key;
    }
  }
}
