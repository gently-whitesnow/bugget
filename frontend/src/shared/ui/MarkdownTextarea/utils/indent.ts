/**
 * Утилиты для работы с отступами (indent/outdent) в contentEditable элементах
 */

export const INDENT = "  ";

/**
 * Добавляет отступ в начало фрагмента и после каждого <br>
 */
export function indentFragmentLines(
  frag: DocumentFragment,
  indent = INDENT
): void {
  // 1) В начало выделения
  frag.insertBefore(document.createTextNode(indent), frag.firstChild);

  // 2) После каждого <br> внутри выделения
  const brs: HTMLBRElement[] = [];
  const walker = document.createTreeWalker(frag, NodeFilter.SHOW_ELEMENT);
  for (let n = walker.nextNode(); n; n = walker.nextNode()) {
    if ((n as Element).tagName === "BR") brs.push(n as HTMLBRElement);
  }

  // вставляем после br (в прямом порядке ок, но можно и в обратном)
  for (const br of brs) {
    br.parentNode?.insertBefore(
      document.createTextNode(indent),
      br.nextSibling
    );
  }
}

/**
 * Удаляет ведущие пробелы из текстового узла
 */
function removeLeadingSpacesFromTextNode(
  node: Text,
  max = INDENT.length
): void {
  const v = node.nodeValue ?? "";
  let k = 0;
  while (k < max && v[k] === " ") k++;
  if (k === 0) return;

  const next = v.slice(k);
  if (next.length) node.nodeValue = next;
  else node.parentNode?.removeChild(node);
}

/**
 * Находит первый текстовый узел в дереве
 */
function findFirstTextNode(root: Node | null): Text | null {
  if (!root) return null;
  if (root.nodeType === Node.TEXT_NODE) return root as Text;

  for (let c = root.firstChild; c; c = c.nextSibling) {
    const t = findFirstTextNode(c);
    if (t) return t;
  }
  return null;
}

/**
 * Удаляет отступ из начала фрагмента и после каждого <br>
 */
export function outdentFragmentLines(
  frag: DocumentFragment,
  indent = INDENT
): void {
  // 1) В начале выделения
  const firstText = findFirstTextNode(frag);
  if (firstText) removeLeadingSpacesFromTextNode(firstText, indent.length);

  // 2) После каждого <br> внутри выделения
  const brs: HTMLBRElement[] = [];
  const walker = document.createTreeWalker(frag, NodeFilter.SHOW_ELEMENT);
  for (let n = walker.nextNode(); n; n = walker.nextNode()) {
    if ((n as Element).tagName === "BR") brs.push(n as HTMLBRElement);
  }

  for (const br of brs) {
    // ищем первый текст после br
    const t =
      findFirstTextNode(br.nextSibling) ??
      findFirstTextNode(br.parentNode?.nextSibling ?? null);
    if (t) removeLeadingSpacesFromTextNode(t, indent.length);
  }
}
