import {
  getLineRange,
  lineEndOf,
  lineStartOf,
  replaceLines,
  type LineChange,
} from "./lineRange";
import type { TextEdit, TextSnapshot } from "./types";

const listItemPattern = /^([ \t]*)(?:([-*+])|(\d{1,9})([.)]))( +)(\[[ xX]\] )?/;
const quotePrefixPattern = /^(?:[ \t]*> ?)+/;
const indentStep = "  ";

type ListItem = {
  indent: number;
  markerWidth: number;
  /** null — маркированный пункт. */
  number: number | null;
  delimiter: string;
  /** Длина «отступ + маркер + пробелы» — всё, что стоит до текста пункта. */
  prefixLength: number;
};

const parseListItem = (line: string): ListItem | null => {
  const match = listItemPattern.exec(line);
  if (!match) return null;
  const [, indent, bullet, number, delimiter, spaces] = match;
  const marker = bullet ?? `${number}${delimiter}`;
  return {
    indent: indent.length,
    markerWidth: marker.length + spaces.length,
    number: number ? Number(number) : null,
    delimiter: delimiter ?? "",
    prefixLength: indent.length + marker.length + spaces.length,
  };
};

const currentLine = (value: string, position: number) => {
  const start = lineStartOf(value, position);
  return { start, text: value.slice(start, lineEndOf(value, position)) };
};

export const isInListItem = ({ value, start }: TextSnapshot): boolean =>
  listItemPattern.test(currentLine(value, start).text);

/**
 * Перенос строки с продолжением списка или цитаты. null — строка не в списке,
 * нужен обычный перенос.
 */
export const continueList = ({
  value,
  start,
  end,
}: TextSnapshot): TextEdit | null => {
  const line = currentLine(value, start);
  let prefix: string;
  let nextPrefix: string;

  const item = listItemPattern.exec(line.text);
  const quote = quotePrefixPattern.exec(line.text);
  if (item) {
    const [full, indent, bullet, number, delimiter, spaces, task] = item;
    prefix = full;
    const marker = bullet ?? `${Number(number) + 1}${delimiter}`;
    nextPrefix = `${indent}${marker}${spaces}${task ? "[ ] " : ""}`;
  } else if (quote) {
    prefix = quote[0];
    nextPrefix = quote[0];
  } else {
    return null;
  }

  if (start - line.start < prefix.length) return null;

  // Перенос на пустом пункте завершает список, как в GitHub и Notion.
  if (start === end && !line.text.slice(prefix.length).trim()) {
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

const leadingSpaces = (line: string): number =>
  line.length - line.trimStart().length;

/** Ближайший пункт выше с отступом не больше заданного; абзац без отступа обрывает список. */
const findItemAbove = (
  lines: string[],
  index: number,
  accepts: (item: ListItem) => boolean
): ListItem | null => {
  for (let i = index - 1; i >= 0; i--) {
    const item = parseListItem(lines[i]);
    if (item && accepts(item)) return item;
    if (!item && lines[i].trim() && leadingSpaces(lines[i]) === 0) return null;
  }
  return null;
};

/**
 * Tab / Shift+Tab. Пункт списка вкладывается под соседа выше на ширину его маркера —
 * иначе CommonMark не распознает вложенность у нумерованного списка.
 */
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
    const oldIndent = leadingSpaces(line);
    const newIndent = nextIndent(allLines, i, direction);
    const item = parseListItem(line);
    const marker = item && renumber(allLines, i, newIndent, item);
    // Маркер переписан — считаем позиции от него целиком, иначе от одного отступа.
    const change: LineChange =
      item && marker
        ? {
            text:
              " ".repeat(newIndent) + marker + line.slice(item.prefixLength),
            oldPrefix: item.prefixLength,
            newPrefix: newIndent + marker.length,
          }
        : {
            text: " ".repeat(newIndent) + line.slice(oldIndent),
            oldPrefix: oldIndent,
            newPrefix: newIndent,
          };
    changed ||= change.text !== line;
    allLines[i] = change.text;
    changes.push(change);
  }

  return changed ? replaceLines(value, from, to, changes, start, end) : null;
};

/**
 * Номер пункта на новом уровне: вложенный список начинается заново с 1, а на прежнем
 * уровне пункт продолжает нумерацию соседа сверху. null — маркер менять не нужно.
 */
const renumber = (
  lines: string[],
  index: number,
  newIndent: number,
  item: ListItem
): string | null => {
  if (item.number === null) return null;

  const sibling = findItemAbove(lines, index, (it) => it.indent <= newIndent);
  const nextNumber =
    sibling && sibling.indent === newIndent && sibling.number !== null
      ? sibling.number + 1
      : 1;
  if (nextNumber === item.number) return null;

  const spaces = " ".repeat(
    item.markerWidth - String(item.number).length - item.delimiter.length
  );
  return `${nextNumber}${item.delimiter}${spaces}`;
};

const nextIndent = (
  lines: string[],
  index: number,
  direction: "in" | "out"
): number => {
  const line = lines[index];
  const indent = leadingSpaces(line);
  if (!line.trim()) return indent;

  const item = parseListItem(line);
  if (!item) {
    return direction === "in"
      ? indent + indentStep.length
      : Math.max(0, indent - indentStep.length);
  }

  if (direction === "in") {
    const sibling = findItemAbove(lines, index, (it) => it.indent <= indent);
    return sibling && sibling.indent === indent
      ? sibling.indent + sibling.markerWidth
      : indent;
  }
  const parent = findItemAbove(lines, index, (it) => it.indent < indent);
  return parent ? parent.indent : 0;
};
