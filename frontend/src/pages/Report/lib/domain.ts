import { normalizeUrl } from "@/shared/lib/markdown";

/** Нестандартные пути к фавиконам; ключ — домен без www. */
const knownFavicons: Record<string, string[]> = {
  "figma.com": [
    "https://static.figma.com/app/icon/1/favicon.png",
    "https://static.figma.com/app/icon/1/favicon.svg",
  ],
};

/** Имя домена для отображения: www.figma.com -> figma. */
export const extractDomainName = (url: string): string => {
  try {
    const urlObj = new URL(url);
    const hostname = urlObj.hostname;
    const parts = hostname.split(".");

    if (parts.length > 2) {
      if (parts[0].toLowerCase() === "www") {
        return parts[1];
      }
      return parts[0];
    } else if (parts.length === 2) {
      return parts[0];
    }
    return hostname;
  } catch {
    return "";
  }
};

/** Домен без www: https://www.figma.com/... -> figma.com. */
export const getNormalizedDomain = (url: string): string | null => {
  try {
    const urlObj = new URL(url);
    const hostname = urlObj.hostname;
    const parts = hostname.split(".");

    if (parts.length > 2 && parts[0].toLowerCase() === "www") {
      return parts.slice(1).join(".");
    }

    return hostname;
  } catch {
    return null;
  }
};

/** Создаёт URL, даже если протокол не указан. */
const ensureUrl = (raw: string): URL => {
  return new URL(normalizeUrl(raw));
};

/**
 * Кандидаты фавикона по приоритету: knownFavicons, затем /favicon.ico и
 * /favicon.png на исходном хосте, на домене без www либо на www-домене.
 */
export const getFaviconUrls = (url: string): string[] => {
  try {
    const urlObj = ensureUrl(url);
    const protocol = urlObj.protocol || "https:";
    const hostname = urlObj.hostname;
    const normalizedDomain = getNormalizedDomain(urlObj.href) ?? hostname;

    const candidates: string[] = [];

    if (knownFavicons[normalizedDomain]) {
      candidates.push(...knownFavicons[normalizedDomain]);
    }

    const origin = `${protocol}//${hostname}`;
    candidates.push(`${origin}/favicon.ico`);
    candidates.push(`${origin}/favicon.png`);

    if (normalizedDomain !== hostname) {
      const normalizedOrigin = `${protocol}//${normalizedDomain}`;
      candidates.push(`${normalizedOrigin}/favicon.ico`);
      candidates.push(`${normalizedOrigin}/favicon.png`);
    } else {
      const wwwOrigin = `${protocol}//www.${hostname}`;
      candidates.push(`${wwwOrigin}/favicon.ico`);
      candidates.push(`${wwwOrigin}/favicon.png`);
    }

    return Array.from(new Set(candidates));
  } catch {
    return [];
  }
};
