/**
 * Идентификатор раздела/разреза аналитики. Хранится в URL `?section=...`.
 */
export type AnalyticsSection = "overview" | "team" | "responsible" | "report";

export const defaultSection: AnalyticsSection = "overview";

const allSections: AnalyticsSection[] = [
  "overview",
  "team",
  "responsible",
  "report",
];

export const parseSection = (s: string | undefined): AnalyticsSection =>
  allSections.includes(s as AnalyticsSection)
    ? (s as AnalyticsSection)
    : defaultSection;
