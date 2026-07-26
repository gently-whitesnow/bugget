import { normalizeUrl } from "@/shared/lib/markdown";

/**
 * Известные домены с нестандартными путями к фавиконам
 * Ключ — нормализованный домен (без www)
 */
const knownFavicons: Record<string, string[]> = {
  "figma.com": [
    "https://static.figma.com/app/icon/1/favicon.png",
    "https://static.figma.com/app/icon/1/favicon.svg",
  ],
  // сюда потом можно добавлять и другие:
  // "github.com": ["https://github.githubassets.com/favicons/favicon.svg"],
};

/**
 * Извлекает имя домена из URL для отображения
 * Пропускает префикс "www" и возвращает основное имя домена
 *
 * @example
 * extractDomainName("https://www.figma.com/...") // "figma"
 * extractDomainName("https://kaiten.io.com/...") // "kaiten"
 * extractDomainName("https://example.com/...") // "example"
 */
export const extractDomainName = (url: string): string => {
  try {
    const urlObj = new URL(url);
    const hostname = urlObj.hostname;
    const parts = hostname.split(".");

    // Если есть поддомен (больше 2 частей)
    if (parts.length > 2) {
      // Пропускаем префикс "www" и берем следующую часть
      // Например: www.figma.com -> figma
      // Иначе берем первую часть: kaiten.io.com -> kaiten
      if (parts[0].toLowerCase() === "www") {
        return parts[1];
      }
      return parts[0];
    } else if (parts.length === 2) {
      // Если нет поддомена (2 части), берем основное имя домена
      // Например: example.com -> example
      return parts[0];
    }
    return hostname;
  } catch {
    return "";
  }
};

/**
 * Получает нормализованный домен из URL (без www для единообразия)
 * Используется для получения фавиконов и других целей
 *
 * @example
 * getNormalizedDomain("https://www.figma.com/...") // "figma.com"
 * getNormalizedDomain("https://figma.com/...") // "figma.com"
 * getNormalizedDomain("https://kaiten.io.com/...") // "kaiten.io.com"
 */
export const getNormalizedDomain = (url: string): string | null => {
  try {
    const urlObj = new URL(url);
    const hostname = urlObj.hostname;
    const parts = hostname.split(".");

    // Если первая часть - "www", убираем её
    if (parts.length > 2 && parts[0].toLowerCase() === "www") {
      return parts.slice(1).join(".");
    }

    return hostname;
  } catch {
    return null;
  }
};

/**
 * Вспомогательная функция: безопасно создать URL, даже если протокол не указан
 */
const ensureUrl = (raw: string): URL => {
  return new URL(normalizeUrl(raw));
};

/**
 * Генерирует URL для фавикона, пробуя несколько вариантов
 *
 * Порядок приоритета:
 * 1) knownFavicons для нормализованного домена (например, Figma)
 * 2) /favicon.ico и /favicon.png на исходном хосте
 * 3) То же для нормализованного домена (без www)
 * 4) То же для www-домена
 * 5) Фоллбек: Google S2 favicon API
 *
 * @param url - исходный URL (может быть полным URL или просто доменом)
 * @returns массив URL для попытки загрузки фавикона (в порядке приоритета)
 */
export const getFaviconUrls = (url: string): string[] => {
  try {
    const urlObj = ensureUrl(url);
    const protocol = urlObj.protocol || "https:";
    const hostname = urlObj.hostname;
    const normalizedDomain = getNormalizedDomain(urlObj.href) ?? hostname;

    const candidates: string[] = [];

    // 1. Известные специальные фавиконы (например, Figma)
    if (knownFavicons[normalizedDomain]) {
      candidates.push(...knownFavicons[normalizedDomain]);
    }

    // 2. Стандартные пути для исходного hostname
    const origin = `${protocol}//${hostname}`;
    candidates.push(`${origin}/favicon.ico`);
    candidates.push(`${origin}/favicon.png`);

    // 3. Если hostname отличается от нормализованного — пробуем и его
    if (normalizedDomain !== hostname) {
      const normalizedOrigin = `${protocol}//${normalizedDomain}`;
      candidates.push(`${normalizedOrigin}/favicon.ico`);
      candidates.push(`${normalizedOrigin}/favicon.png`);
    } else {
      // 4. Если домен без www — пробуем www.<hostname>
      // Например: figma.com -> www.figma.com
      const wwwOrigin = `${protocol}//www.${hostname}`;
      candidates.push(`${wwwOrigin}/favicon.ico`);
      candidates.push(`${wwwOrigin}/favicon.png`);
    }

    // Убираем дубликаты, сохраняя порядок
    return Array.from(new Set(candidates));
  } catch {
    return [];
  }
};
