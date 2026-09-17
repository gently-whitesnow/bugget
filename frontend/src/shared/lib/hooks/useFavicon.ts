import { useState, useEffect } from "react";
import { getFaviconUrls } from "../domain";

// Перебирает варианты URL фавикона (с www и без), отдаёт первый загрузившийся.
export const useFavicon = (
  url: string,
  timeout: number = 2000
): string | null => {
  const [faviconUrl, setFaviconUrl] = useState<string | null>(null);

  useEffect(() => {
    const loadFavicon = async () => {
      try {
        const faviconUrls = getFaviconUrls(url);
        if (faviconUrls.length === 0) {
          setFaviconUrl(null);
          return;
        }

        let loaded = false;
        for (const faviconUrl of faviconUrls) {
          const img = new Image();
          const loadPromise = new Promise<void>((resolve) => {
            const timeoutId = setTimeout(() => {
              resolve();
            }, timeout);

            img.onload = () => {
              clearTimeout(timeoutId);
              if (!loaded) {
                setFaviconUrl(faviconUrl);
                loaded = true;
              }
              resolve();
            };
            img.onerror = () => {
              clearTimeout(timeoutId);
              resolve();
            };
            img.src = faviconUrl;
          });

          await loadPromise;

          if (loaded) break;
        }

        if (!loaded) {
          setFaviconUrl(null);
        }
      } catch {
        // Невалидный URL — фавикона нет.
        setFaviconUrl(null);
      }
    };

    loadFavicon();
  }, [url, timeout]);

  return faviconUrl;
};
