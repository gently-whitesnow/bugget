import { normalizeUrl } from "@/shared/lib/markdown";

const linkClassName =
  "text-primary underline break-all hover:text-primary-focus cursor-pointer";

const looksLikeUrl = (text: string): boolean => {
  const normalizedText = text.trim();
  return (
    normalizedText.startsWith("http://") ||
    normalizedText.startsWith("https://") ||
    (normalizedText.includes(".") && !normalizedText.includes(" "))
  );
};

/** Валидирует и нормализует URL из буфера обмена */
export const validateAndNormalizeUrl = (
  clipboardText: string
): string | null => {
  try {
    const normalizedText = clipboardText.trim();
    if (!looksLikeUrl(normalizedText)) {
      return null;
    }

    const url = normalizeUrl(normalizedText);
    // new URL бросает на невалидной строке
    new URL(url);
    return url;
  } catch {
    return null;
  }
};

export const createLinkElement = (
  url: string,
  text: string
): HTMLAnchorElement => {
  const link = document.createElement("a");
  link.href = url;
  link.target = "_blank";
  link.rel = "noopener noreferrer";
  link.className = linkClassName;
  link.setAttribute("data-link-url", url);
  link.textContent = text;
  return link;
};
