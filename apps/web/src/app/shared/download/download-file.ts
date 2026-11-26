/**
 * Віддає файл браузеру на завантаження: тимчасова адреса `blob:` і посилання з `download` (назва
 * файлу), яке натискаємо й одразу прибираємо.
 *
 *   downloadFile('Донори 2026.xlsx', bytes, XLSX_TYPE);
 */
export function downloadFile(fileName: string, content: BlobPart, type: string): void {
  const url = URL.createObjectURL(new Blob([content], { type }));
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.style.display = 'none';
  document.body.append(link);
  link.click();
  link.remove();
  // Звільняємо адресу, коли браузер уже почав завантаження.
  setTimeout(() => URL.revokeObjectURL(url));
}
