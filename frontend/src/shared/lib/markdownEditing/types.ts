/** Текст поля и выделение; start <= end. */
export type TextSnapshot = {
  value: string;
  start: number;
  end: number;
};

/** Замена диапазона [from, to) на insert и выделение после замены. */
export type TextEdit = {
  from: number;
  to: number;
  insert: string;
  selectionStart: number;
  selectionEnd: number;
};
