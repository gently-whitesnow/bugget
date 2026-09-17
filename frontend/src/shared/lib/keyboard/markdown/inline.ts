import type { TextEdit, TextSnapshot } from "./types";

export type InlineFormat = "bold" | "italic" | "strikethrough" | "code";

const MARKERS: Record<InlineFormat, string[]> = {
  bold: ["**"],
  italic: ["_", "*"],
  strikethrough: ["~~"],
  code: ["`"],
};

const isWordChar = (char: string | undefined): boolean =>
  char !== undefined && /[\p{L}\p{N}]/u.test(char);

const runLeft = (value: string, position: number, char: string): number => {
  let count = 0;
  while (value[position - 1 - count] === char) count++;
  return count;
};

const runRight = (value: string, position: number, char: string): number => {
  let count = 0;
  while (value[position + count] === char) count++;
  return count;
};

const isWrappedBy = (
  value: string,
  start: number,
  end: number,
  marker: string,
  format: InlineFormat
): boolean => {
  const before = runLeft(value, start, marker[0]);
  const after = runRight(value, end, marker[0]);
  // Курсив одним символом: чётная серия звёздочек — это жирный, а не курсив.
  if (format === "italic") return before % 2 === 1 && after % 2 === 1;
  return before >= marker.length && after >= marker.length;
};

const pickWrapMarker = (
  value: string,
  start: number,
  end: number,
  format: InlineFormat
): string => {
  // `_` внутри слова CommonMark курсивом не считает, `*` — считает.
  if (
    format === "italic" &&
    (isWordChar(value[start - 1]) || isWordChar(value[end]))
  ) {
    return "*";
  }
  return MARKERS[format][0];
};

const toggleRange = (
  value: string,
  start: number,
  end: number,
  format: InlineFormat
): TextEdit => {
  const selected = value.slice(start, end);

  for (const marker of MARKERS[format]) {
    const size = marker.length;
    if (isWrappedBy(value, start, end, marker, format)) {
      return {
        from: start - size,
        to: end + size,
        insert: selected,
        selectionStart: start - size,
        selectionEnd: end - size,
      };
    }
    if (
      selected.length >= size * 2 &&
      selected.startsWith(marker) &&
      selected.endsWith(marker) &&
      isWrappedBy(value, start + size, end - size, marker, format)
    ) {
      return {
        from: start,
        to: end,
        insert: selected.slice(size, -size),
        selectionStart: start,
        selectionEnd: end - size * 2,
      };
    }
  }

  let marker = pickWrapMarker(value, start, end, format);
  let padding = "";
  if (format === "code" && selected.includes("`")) {
    marker = "``";
    padding = selected.startsWith("`") || selected.endsWith("`") ? " " : "";
  }
  const offset = marker.length + padding.length;
  return {
    from: start,
    to: end,
    insert: `${marker}${padding}${selected}${padding}${marker}`,
    selectionStart: start + offset,
    selectionEnd: end + offset,
  };
};

export const toggleInlineFormat = (
  { value, start, end }: TextSnapshot,
  format: InlineFormat
): TextEdit | null => {
  if (start !== end) {
    // Двойной клик в части браузеров захватывает пробел после слова: `**слово **` не жирный.
    const selected = value.slice(start, end);
    const trimmedStart =
      start + (selected.length - selected.trimStart().length);
    const trimmedEnd = end - (selected.length - selected.trimEnd().length);
    if (trimmedStart >= trimmedEnd) return null;
    return toggleRange(value, trimmedStart, trimmedEnd, format);
  }

  for (const marker of MARKERS[format]) {
    const size = marker.length;
    if (
      value.slice(start - size, start) === marker &&
      value.slice(start, start + size) === marker &&
      isWrappedBy(value, start, start, marker, format)
    ) {
      return {
        from: start - size,
        to: start + size,
        insert: "",
        selectionStart: start - size,
        selectionEnd: start - size,
      };
    }
  }

  if (isWordChar(value[start - 1]) && isWordChar(value[start])) {
    let wordStart = start;
    let wordEnd = start;
    while (isWordChar(value[wordStart - 1])) wordStart--;
    while (isWordChar(value[wordEnd])) wordEnd++;
    const edit = toggleRange(value, wordStart, wordEnd, format);
    const cursor = start + edit.selectionStart - wordStart;
    return { ...edit, selectionStart: cursor, selectionEnd: cursor };
  }

  const marker = pickWrapMarker(value, start, start, format);
  const cursor = start + marker.length;
  return {
    from: start,
    to: start,
    insert: marker + marker,
    selectionStart: cursor,
    selectionEnd: cursor,
  };
};

const URL_PATTERN = /^https?:\/\/\S+$/i;
const LINK_TAIL_PATTERN = /^\]\([^)\s]*\)/;
const URL_PLACEHOLDER = "url";

export const toggleLink = ({ value, start, end }: TextSnapshot): TextEdit => {
  const selected = value.slice(start, end);
  const tail = LINK_TAIL_PATTERN.exec(value.slice(end));

  if (selected && value[start - 1] === "[" && tail) {
    return {
      from: start - 1,
      to: end + tail[0].length,
      insert: selected,
      selectionStart: start - 1,
      selectionEnd: end - 1,
    };
  }

  if (URL_PATTERN.test(selected)) {
    return {
      from: start,
      to: end,
      insert: `[](${selected})`,
      selectionStart: start + 1,
      selectionEnd: start + 1,
    };
  }

  if (!selected) {
    return {
      from: start,
      to: end,
      insert: "[]()",
      selectionStart: start + 1,
      selectionEnd: start + 1,
    };
  }

  const urlStart = start + selected.length + 3;
  return {
    from: start,
    to: end,
    insert: `[${selected}](${URL_PLACEHOLDER})`,
    selectionStart: urlStart,
    selectionEnd: urlStart + URL_PLACEHOLDER.length,
  };
};
