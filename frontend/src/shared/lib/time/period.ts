/**
 * Дискретные периоды агрегации аналитики (см. AnalyticsPeriodKey / PeriodKey
 * в OpenAPI-контракте analytics).
 */
export type AnalyticsPeriod = "7d" | "30d" | "60d" | "180d" | "360d" | "all";

export const defaultPeriod: AnalyticsPeriod = "7d";

export const allPeriods: AnalyticsPeriod[] = [
  "7d",
  "30d",
  "60d",
  "180d",
  "360d",
  "all",
];

export const parsePeriod = (s: string | undefined): AnalyticsPeriod =>
  allPeriods.includes(s as AnalyticsPeriod)
    ? (s as AnalyticsPeriod)
    : defaultPeriod;

export const periodToQuery = (p: AnalyticsPeriod): string => p;

export const periodLabel = (p: AnalyticsPeriod): string => {
  switch (p) {
    case "7d":
      return "За 7 дней";
    case "30d":
      return "За 30 дней";
    case "60d":
      return "За 60 дней";
    case "180d":
      return "За 180 дней";
    case "360d":
      return "За 360 дней";
    case "all":
      return "За всё время";
  }
};
