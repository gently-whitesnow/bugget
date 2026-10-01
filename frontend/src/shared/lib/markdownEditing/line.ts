/** Таб — четыре колонки, как в CommonMark: меряем отступ так же, как потом отрисуется. */
const tabWidth = 4;

const linePattern =
  /^((?:[ \t]*> ?)*)([ \t]*)(?:(#{1,6})( +)|([-*+])( +)|(\d{1,9})([.)])( +))?(\[[ xX]\] )?/;

export type LineMarker =
  | { kind: "heading"; level: number }
  | { kind: "bullet"; bullet: string }
  | { kind: "ordered"; number: number; delimiter: string };

export type ParsedLine = {
  /** Префиксы цитат: «> », «> > ». Отступ и маркер идут уже после них. */
  quote: string;
  /** Ширина отступа в колонках: таб считается за четыре. */
  indent: number;
  /** Сам отступ как он записан — нужен, чтобы не трогать чужие табы. */
  indentText: string;
  marker: LineMarker | null;
  /** Маркер с пробелами после него: «- », «1. », «## ». */
  markerText: string;
  /** Чекбокс пункта: «[ ] » или «[x] ». */
  task: string;
  content: string;
  /** Длина всего служебного начала строки в символах. */
  prefixLength: number;
};

export const indentWidth = (text: string): number =>
  [...text].reduce((width, char) => width + (char === "\t" ? tabWidth : 1), 0);

export const parseLine = (line: string): ParsedLine => {
  const match = linePattern.exec(line)!;
  const [
    full,
    quote,
    indentText,
    heading,
    headingSpaces,
    bullet,
    bulletSpaces,
    number,
    delimiter,
    numberSpaces,
    task = "",
  ] = match;

  let marker: LineMarker | null = null;
  let markerText = "";
  if (heading) {
    marker = { kind: "heading", level: heading.length };
    markerText = heading + headingSpaces;
  } else if (bullet) {
    marker = { kind: "bullet", bullet };
    markerText = bullet + bulletSpaces;
  } else if (number) {
    marker = { kind: "ordered", number: Number(number), delimiter };
    markerText = number + delimiter + numberSpaces;
  }

  return {
    quote,
    indent: indentWidth(indentText),
    indentText,
    marker,
    markerText,
    task,
    content: line.slice(full.length),
    prefixLength: full.length,
  };
};

/** Пункт списка: заголовок списком не считается. */
export const isListItem = (line: ParsedLine): boolean =>
  line.marker !== null && line.marker.kind !== "heading";
