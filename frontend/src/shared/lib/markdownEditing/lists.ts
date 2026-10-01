import {
  getLineRange,
  lineEndOf,
  lineStartOf,
  replaceLines,
  type LineChange,
} from "./lineRange";
import { isListItem, parseLine, type ParsedLine } from "./line";
import type { TextEdit, TextSnapshot } from "./types";

const indentStep = "  ";

const currentLine = (value: string, position: number) => {
  const start = lineStartOf(value, position);
  return { start, text: value.slice(start, lineEndOf(value, position)) };
};

const parseListLine = (line: string): ParsedLine | null => {
  const parsed = parseLine(line);
  return isListItem(parsed) ? parsed : null;
};

export const isInListItem = ({ value, start }: TextSnapshot): boolean =>
  parseListLine(currentLine(value, start).text) !== null;

/** null — строка не в списке, владельцу нужен обычный перенос. */
export const continueList = ({
  value,
  start,
  end,
}: TextSnapshot): TextEdit | null => {
  const line = currentLine(value, start);
  const parsed = parseLine(line.text);
  const marker = parsed.marker;

  let nextPrefix: string;
  if (marker && marker.kind !== "heading") {
    const nextMarker =
      marker.kind === "ordered"
        ? parsed.markerText.replace(
            String(marker.number),
            String(marker.number + 1)
          )
        : parsed.markerText;
    const nextTask = parsed.task ? "[ ] " : "";
    nextPrefix = parsed.quote + parsed.indentText + nextMarker + nextTask;
  } else if (parsed.quote) {
    nextPrefix = parsed.quote;
  } else {
    return null;
  }

  if (start - line.start < parsed.prefixLength) return null;

  // Перенос на пустом пункте завершает список, как в GitHub и Notion.
  if (start === end && !parsed.content.trim()) {
    return {
      from: line.start,
      to: line.start + line.text.length,
      insert: "",
      selectionStart: line.start,
      selectionEnd: line.start,
    };
  }

  const cursor = start + 1 + nextPrefix.length;
  return {
    from: start,
    to: end,
    insert: `\n${nextPrefix}`,
    selectionStart: cursor,
    selectionEnd: cursor,
  };
};

/** Ближайший пункт выше с отступом не больше заданного; абзац без отступа обрывает список. */
const findItemAbove = (
  lines: string[],
  index: number,
  accepts: (item: ParsedLine) => boolean
): ParsedLine | null => {
  for (let i = index - 1; i >= 0; i--) {
    const item = parseListLine(lines[i]);
    if (item && accepts(item)) return item;
    if (!item && lines[i].trim() && parseLine(lines[i]).indent === 0) {
      return null;
    }
  }
  return null;
};

const markerWidth = (item: ParsedLine): number => item.markerText.length;

/** Пункт вкладывается на ширину маркера соседа: так вложенность видит CommonMark. */
export const shiftIndent = (
  { value, start, end }: TextSnapshot,
  direction: "in" | "out"
): TextEdit | null => {
  const { from, to } = getLineRange(value, start, end);
  const allLines = value.split("\n");
  const firstIndex = value.slice(0, from).split("\n").length - 1;
  const count = value.slice(from, to).split("\n").length;
  let changed = false;

  const changes: LineChange[] = [];
  for (let i = firstIndex; i < firstIndex + count; i++) {
    const line = allLines[i];
    const parsed = parseLine(line);
    // Отступ пишем пробелами: тронутая строка нормализуется, чужие табы не трогаем.
    const oldIndentLength = parsed.quote.length + parsed.indentText.length;
    const newIndent = nextIndent(allLines, i, direction);
    const item = isListItem(parsed) ? parsed : null;
    const marker = item && renumber(allLines, i, newIndent, item);
    const indent = parsed.quote + " ".repeat(newIndent);
    const change: LineChange =
      item && marker
        ? {
            text: indent + marker + item.task + line.slice(item.prefixLength),
            oldPrefix: item.prefixLength,
            newPrefix: indent.length + marker.length + item.task.length,
          }
        : {
            text: indent + line.slice(oldIndentLength),
            oldPrefix: oldIndentLength,
            newPrefix: indent.length,
          };
    changed ||= change.text !== line;
    allLines[i] = change.text;
    changes.push(change);
  }

  return changed ? replaceLines(value, from, to, changes, start, end) : null;
};

/** Вложенный список начинается с 1, на прежнем уровне — следующим за соседом сверху. */
const renumber = (
  lines: string[],
  index: number,
  newIndent: number,
  item: ParsedLine
): string | null => {
  if (item.marker?.kind !== "ordered") return null;
  const { number, delimiter } = item.marker;

  const sibling = findItemAbove(lines, index, (it) => it.indent <= newIndent);
  const siblingMarker = sibling?.marker;
  const nextNumber =
    sibling && sibling.indent === newIndent && siblingMarker?.kind === "ordered"
      ? siblingMarker.number + 1
      : 1;
  if (nextNumber === number) return null;

  const spaces = " ".repeat(
    markerWidth(item) - String(number).length - delimiter.length
  );
  return `${nextNumber}${delimiter}${spaces}`;
};

const nextIndent = (
  lines: string[],
  index: number,
  direction: "in" | "out"
): number => {
  const line = lines[index];
  const parsed = parseLine(line);
  const indent = parsed.indent;
  if (!line.trim()) return indent;

  const item = isListItem(parsed) ? parsed : null;
  if (!item) {
    return direction === "in"
      ? indent + indentStep.length
      : Math.max(0, indent - indentStep.length);
  }

  if (direction === "in") {
    const sibling = findItemAbove(lines, index, (it) => it.indent <= indent);
    return sibling && sibling.indent === indent
      ? sibling.indent + markerWidth(sibling)
      : indent;
  }
  const parent = findItemAbove(lines, index, (it) => it.indent < indent);
  return parent ? parent.indent : 0;
};
