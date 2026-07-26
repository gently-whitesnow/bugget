import { useEffect, useState, RefObject } from "react";
import { normalizeUrl } from "@/shared/lib/markdown";

type LinkPreviewData = {
  url: string;
  linkElement: HTMLElement;
};

type UseLinkPreviewResult = {
  linkPreview: LinkPreviewData | null;
  closeLinkPreview: () => void;
};

/**
 * Хук для обработки кликов по ссылкам в contentEditable элементах
 * и управления состоянием превью ссылок.
 */
export const useLinkPreview = (
  elementRef: RefObject<HTMLElement | null>
): UseLinkPreviewResult => {
  const [linkPreview, setLinkPreview] = useState<LinkPreviewData | null>(null);

  useEffect(() => {
    const element = elementRef.current;
    if (!element) return;

    const handleLinkClick = (event: MouseEvent) => {
      const target = event.target;

      let link: HTMLElement | null = null;
      if (target instanceof Element) {
        link = target.closest("a");
      }

      if (!link) return;

      event.preventDefault();
      event.stopPropagation();

      const url =
        link.getAttribute("href") || link.getAttribute("data-link-url");

      if (!url) return;

      // Валидируем URL
      try {
        const normalizedUrl = normalizeUrl(url);

        new URL(normalizedUrl);

        setLinkPreview({ url: normalizedUrl, linkElement: link });
      } catch {
        console.warn("Invalid URL in link:", url);
      }
    };

    // Используем capture phase для перехвата кликов до contenteditable
    element.addEventListener("click", handleLinkClick, true);

    return () => {
      element.removeEventListener("click", handleLinkClick, true);
    };
  }, [elementRef]);

  const closeLinkPreview = () => {
    setLinkPreview(null);
  };

  return {
    linkPreview,
    closeLinkPreview,
  };
};
