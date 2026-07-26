import {
  htmlToMarkdown,
  markdownToHtml,
  parseMarkdownLinks,
} from "@/shared/lib/markdown";

/**
 * Обрабатывает markdown-ссылки в тексте при потере фокуса
 * Преобразует markdown-синтаксис [text](url) в HTML-ссылки
 * Возвращает HTML строку для установки в innerHTML
 */
export const getMarkdownLinksOnBlur = (element: HTMLDivElement): string => {
  const currentMarkdown = htmlToMarkdown(element);
  const markdownLinks = parseMarkdownLinks(currentMarkdown);

  if (markdownLinks.length > 0) {
    return markdownToHtml(currentMarkdown);
  }

  // Если нет ссылок, возвращаем HTML представление markdown
  return markdownToHtml(currentMarkdown);
};
