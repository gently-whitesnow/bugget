import {
  getLineRange,
  lineEndOf,
  lineStartOf,
  replaceLines,
  type LineChange,
} from "./lineRange";
import { parseLine, type LineMarker } from "./line";
import type { TextEdit, TextSnapshot } from "./types";

export type LineFormat =
  | { kind: "heading"; level: 1 | 2 | 3 }
  | { kind: "bulletList" }
  | { kind: "orderedList" };

const hasFormat = (marker: LineMarker | null, format: LineFormat): boolean => {
  if (!marker) return false;
  switch (format.kind) {
    case "heading":
      return marker.kind === "heading" && marker.level === format.level;
    case "bulletList":
      return marker.kind === "bullet";
    case "orderedList":
      return marker.kind === "ordered";
  }
};

const markerFor = (format: LineFormat, index: number): string => {
  switch (format.kind) {
    case "heading":
      return `${"#".repeat(format.level)} `;
    case "bulletList":
      return "- ";
    case "orderedList":
      return `${index}. `;
  }
};

/** Пустые строки внутри выделения не размечаем — кроме случая, когда строка одна. */
const targetIndexes = (lines: string[]): number[] =>
  lines.length === 1
    ? [0]
    : lines.flatMap((line, i) => (line.trim() ? [i] : []));

export const toggleLineFormat = (
  { value, start, end }: TextSnapshot,
  format: LineFormat
): TextEdit => {
  const { from, to } = getLineRange(value, start, end);
  const lines = value.slice(from, to).split("\n");
  const parsed = lines.map(parseLine);
  const targets = new Set(targetIndexes(lines));
  const isActive = [...targets].every((i) =>
    hasFormat(parsed[i].marker, format)
  );

  let ordinal = 1;
  const changes: LineChange[] = parsed.map((line, i) => {
    // Цитата и отступ остаются на месте: меняется только маркер после них.
    const lead = line.quote + line.indentText;
    const oldPrefix = lead.length + line.markerText.length;
    if (!targets.has(i)) {
      return { text: lines[i], oldPrefix, newPrefix: oldPrefix };
    }
    const nextMarker = isActive ? "" : markerFor(format, ordinal++);
    const content = line.task + line.content;
    return {
      text: lead + nextMarker + content,
      oldPrefix,
      newPrefix: lead.length + nextMarker.length,
    };
  });

  return replaceLines(value, from, to, changes, start, end);
};

const quotePattern = /^[ \t]*> ?/;

export const toggleQuote = ({ value, start, end }: TextSnapshot): TextEdit => {
  const { from, to } = getLineRange(value, start, end);
  const lines = value.slice(from, to).split("\n");
  const targets = new Set(targetIndexes(lines));
  const isActive = [...targets].every((i) => quotePattern.test(lines[i]));

  const changes: LineChange[] = lines.map((line, i) => {
    if (!targets.has(i)) return { text: line, oldPrefix: 0, newPrefix: 0 };
    if (isActive) {
      const prefix = quotePattern.exec(line)![0].length;
      return { text: line.slice(prefix), oldPrefix: prefix, newPrefix: 0 };
    }
    return { text: `> ${line}`, oldPrefix: 0, newPrefix: 2 };
  });

  return replaceLines(value, from, to, changes, start, end);
};

const fenceOpenPattern = /^[ \t]*```[^`]*$/;
const fenceClosePattern = /^[ \t]*```[ \t]*$/;
const fence = "```";

export const toggleCodeBlock = ({
  value,
  start,
  end,
}: TextSnapshot): TextEdit => {
  const { from, to } = getLineRange(value, start, end);
  const selectionEnd = Math.min(end, to);

  if (from > 0 && to < value.length) {
    const openStart = lineStartOf(value, from - 1);
    const closeEnd = lineEndOf(value, to + 1);
    const openLine = value.slice(openStart, from - 1);
    const closeLine = value.slice(to + 1, closeEnd);
    if (fenceOpenPattern.test(openLine) && fenceClosePattern.test(closeLine)) {
      const shift = from - openStart;
      return {
        from: openStart,
        to: closeEnd,
        insert: value.slice(from, to),
        selectionStart: start - shift,
        selectionEnd: selectionEnd - shift,
      };
    }
  }

  const shift = fence.length + 1;
  return {
    from,
    to,
    insert: `${fence}\n${value.slice(from, to)}\n${fence}`,
    selectionStart: start + shift,
    selectionEnd: selectionEnd + shift,
  };
};
