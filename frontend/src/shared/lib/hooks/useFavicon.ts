import { useState, useEffect } from "react";
import { getFaviconUrls } from "../domain";

/**
 * Хук для загрузки фавикона по URL
 * Пробует несколько вариантов URL (с www и без) и возвращает первый успешно загруженный
 *
 * @param url - URL для получения фавикона
 * @param timeout - таймаут загрузки в миллисекундах (по умолчанию 2000)
 * @returns URL фавикона или null, если загрузка не удалась
 */
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

        // Пробуем загрузить фавикон, перебирая варианты URL
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

        // Если ни один вариант не загрузился, оставляем null
        if (!loaded) {
          setFaviconUrl(null);
        }
      } catch {
        // Если URL невалидный, оставляем null
        setFaviconUrl(null);
      }
    };

    loadFavicon();
  }, [url, timeout]);

  return faviconUrl;
};
