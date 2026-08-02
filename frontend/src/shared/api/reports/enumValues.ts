import type { components } from "@/shared/api/generated/reports";

type CompleteEnumValues<T extends string, R extends Record<string, T>> = [
  T,
] extends [R[keyof R]]
  ? R
  : never;

/**
 * Регистр значений закрытого OpenAPI-union. Ограничение на значения запрещает
 * лишний литерал, а обратная проверка union гарантирует, что перечислены все
 * члены generated-типа.
 */
export const defineEnumValues =
  <T extends string>() =>
  <const R extends Record<string, T>>(values: CompleteEnumValues<T, R>): R =>
    values;

export type BugStatuses = components["schemas"]["BugStatus"];
export const BugStatuses = defineEnumValues<BugStatuses>()({
  OPEN: "open",
  VERIFIED: "verified",
  REJECTED: "rejected",
  FIXED: "fixed",
});

export type ReportStatuses = components["schemas"]["ReportStatus"];
export const ReportStatuses = defineEnumValues<ReportStatuses>()({
  BACKLOG: "backlog",
  RESOLVED: "resolved",
  FIX: "fix",
  REJECTED: "rejected",
  TEST: "test",
});

export type AttachmentTypes = components["schemas"]["AttachType"];
export const AttachmentTypes = defineEnumValues<AttachmentTypes>()({
  FACT: "fact",
  EXPECT: "expected",
  COMMENT: "comment",
  BUG_STEP: "bug_step",
});

export type CreatorTypes = components["schemas"]["CreatorType"];
export const CreatorTypes = defineEnumValues<CreatorTypes>()({
  USER: "user",
  SYSTEM: "system",
  TG_BETA_TESTER: "tg_beta_tester",
});

export type CommentAudiences = components["schemas"]["CommentAudience"];
export const CommentAudiences = defineEnumValues<CommentAudiences>()({
  INTERNAL: "internal",
  EXTERNAL: "external",
});
