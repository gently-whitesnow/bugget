/**
 * Нормализует URL (добавляет https:// если отсутствует протокол)
 */
export const normalizeUrl = (url: string): string => {
  if (url.startsWith("http://") || url.startsWith("https://")) {
    return url;
  }
  return `https://${url}`;
};
