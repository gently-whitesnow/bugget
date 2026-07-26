/**
 * Утилиты для ISO 8601 week-strings формата `YYYY-Www`.
 * Используется бэкендом analytics (`phase_trends_weekly[].iso_week`).
 */

export type IsoWeek = {
  year: number;
  week: number;
};

/**
 * Парсит ISO-неделю `YYYY-Www`. Бросает Error для невалидной строки.
 * @example parseIsoWeek("2026-W18") => { year: 2026, week: 18 }
 */
export const parseIsoWeek = (s: string): IsoWeek => {
  const match = /^(\d{4})-W(\d{2})$/.exec(s);
  if (!match) {
    throw new Error(`Invalid ISO week string: ${s}`);
  }
  const year = Number(match[1]);
  const week = Number(match[2]);
  if (week < 1 || week > 53) {
    throw new Error(`ISO week out of range (1..53): ${s}`);
  }
  return { year, week };
};

/**
 * Человекочитаемый ярлык ISO-недели.
 * @example formatIsoWeekLabel({ year: 2026, week: 18 }) => "W18 · 2026"
 */
export const formatIsoWeekLabel = ({ year, week }: IsoWeek): string => {
  const ww = week.toString().padStart(2, "0");
  return `W${ww} · ${year}`;
};
