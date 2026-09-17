import type { TextEdit } from "./types";

export const lineStartOf = (value: string, position: number): number =>
  value.lastIndexOf("\n", position - 1) + 1;

export const lineEndOf = (value: string, position: number): number => {
  const index = value.indexOf("\n", position);
  return index === -1 ? value.length : index;
};

/** Границы строк, которые задевает выделение [start, end). */
export const getLineRange = (value: string, start: number, end: number) => {
  // Выделение до начала строки (тройной клик) эту строку не захватывает.
  const effectiveEnd = end > start && value[end - 1] === "\n" ? end - 1 : end;
  return {
    from: lineStartOf(value, start),
    to: lineEndOf(value, Math.max(start, effectiveEnd)),
  };
};

export type LineChange = {
  text: string;
  /** Длина служебного начала строки (отступ, маркер) до и после правки. */
  oldPrefix: number;
  newPrefix: number;
};

/**
 * Заменяет строки блока [from, to) и переносит выделение: позиция в тексте строки
 * сдвигается вместе с ним, позиция внутри старого маркера прижимается к новому.
 */
export const replaceLines = (
  value: string,
  from: number,
  to: number,
  changes: LineChange[],
  start: number,
  end: number
): TextEdit => {
  const oldLines = value.slice(from, to).split("\n");
  const collapsed = start === end;

  const mapPosition = (position: number): number => {
    let oldOffset = from;
    let newOffset = from;
    for (let i = 0; i < changes.length; i++) {
      const lineEnd = oldOffset + oldLines[i].length;
      if (position <= lineEnd || i === changes.length - 1) {
        const { oldPrefix, newPrefix } = changes[i];
        const column = Math.max(0, position - oldOffset);
        if (!collapsed && column === 0) return newOffset;
        if (column >= oldPrefix) {
          return newOffset + column - oldPrefix + newPrefix;
        }
        return (
          newOffset + (collapsed ? newPrefix : Math.min(column, newPrefix))
        );
      }
      oldOffset = lineEnd + 1;
      newOffset += changes[i].text.length + 1;
    }
    return newOffset;
  };

  return {
    from,
    to,
    insert: changes.map((change) => change.text).join("\n"),
    selectionStart: mapPosition(start),
    selectionEnd: mapPosition(end),
  };
};
