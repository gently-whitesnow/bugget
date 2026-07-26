export type MarkdownLink = {
  text: string;
  url: string;
  startIndex: number;
  endIndex: number;
  fullMatch: string;
};

const escapeHtml = (value: string): string =>
  value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");

/**
 * Парсит Markdown ссылки в формате [текст](url) из строки
 */
export const parseMarkdownLinks = (text: string): MarkdownLink[] => {
  const links: MarkdownLink[] = [];
  // Регулярное выражение для поиска Markdown ссылок [текст](url)
  const markdownLinkRegex = /\[([^\]]+)\]\(([^)]+)\)/g;
  let match;

  while ((match = markdownLinkRegex.exec(text)) !== null) {
    links.push({
      text: match[1],
      url: match[2],
      startIndex: match.index,
      endIndex: match.index + match[0].length,
      fullMatch: match[0],
    });
  }

  return links;
};

/**
 * Проверяет, является ли строка валидным URL
 */
export const isValidUrl = (string: string): boolean => {
  try {
    const url = new URL(string);
    return url.protocol === "http:" || url.protocol === "https:";
  } catch (err) {
    console.error(err);
    return false;
  }
};

/**
 * Нормализует URL (добавляет https:// если отсутствует протокол)
 */
export const normalizeUrl = (url: string): string => {
  if (url.startsWith("http://") || url.startsWith("https://")) {
    return url;
  }
  return `https://${url}`;
};

const toSafeHref = (url: string): string => {
  const normalizedUrl = normalizeUrl(url.trim());

  try {
    const parsedUrl = new URL(normalizedUrl);
    if (parsedUrl.protocol === "http:" || parsedUrl.protocol === "https:") {
      return parsedUrl.toString();
    }
  } catch {
    // no-op: invalid URL is rendered as plain text below
  }

  return "";
};

// Конвертирует Markdown в HTML для отображения
export const markdownToHtml = (text: string): string => {
  if (!text) return "";

  const links = parseMarkdownLinks(text);
  if (links.length === 0) {
    return escapeHtml(text).replace(/\n/g, "<br>");
  }

  let html = "";
  let lastIndex = 0;

  links.forEach((link) => {
    // Добавляем текст до ссылки
    if (link.startIndex > lastIndex) {
      html += escapeHtml(text.substring(lastIndex, link.startIndex)).replace(
        /\n/g,
        "<br>"
      );
    }

    // Добавляем ссылку
    const href = toSafeHref(link.url);
    const escapedText = escapeHtml(link.text);

    if (href) {
      const escapedHref = escapeHtml(href);
      html += `<a href="${escapedHref}" target="_blank" rel="noopener noreferrer" class="text-primary underline break-all hover:text-primary-focus cursor-pointer" data-link-url="${escapedHref}">${escapedText}</a>`;
    } else {
      html += escapedText;
    }

    lastIndex = link.endIndex;
  });

  // Добавляем оставшийся текст
  if (lastIndex < text.length) {
    html += escapeHtml(text.substring(lastIndex)).replace(/\n/g, "<br>");
  }

  return html;
};

// Конвертирует HTML обратно в Markdown
export const htmlToMarkdown = (element: HTMLElement): string => {
  let markdown = "";

  const processNode = (node: Node, isFirstInBlock = false) => {
    if (node.nodeType === Node.TEXT_NODE) {
      markdown += node.textContent || "";
    } else if (node.nodeType === Node.ELEMENT_NODE) {
      const el = node as HTMLElement;

      if (el.tagName === "A") {
        const href = el.getAttribute("href") || "";
        const text = el.textContent || "";
        markdown += `[${text}](${href})`;
      } else if (el.tagName === "BR") {
        markdown += "\n";
      } else if (el.tagName === "DIV" || el.tagName === "P") {
        // DIV и P теги создают перенос строки
        // Добавляем перенос перед содержимым, если это не первый элемент
        if (
          !isFirstInBlock &&
          markdown.length > 0 &&
          !markdown.endsWith("\n")
        ) {
          markdown += "\n";
        }
        // Обрабатываем дочерние узлы
        const childNodes = Array.from(el.childNodes);
        childNodes.forEach((child, index) => {
          processNode(child, index === 0);
        });
        // Добавляем перенос после блока, если есть следующий элемент
        if (el.nextSibling) {
          markdown += "\n";
        }
      } else {
        // Обрабатываем дочерние узлы
        Array.from(el.childNodes).forEach((child, index) => {
          processNode(child, index === 0);
        });
      }
    }
  };

  const childNodes = Array.from(element.childNodes);
  childNodes.forEach((child, index) => {
    processNode(child, index === 0);
  });

  // Нормализуем результат: убираем лишние переносы строк в начале и конце
  // и заменяем множественные переносы на один
  return markdown.trim();
};
