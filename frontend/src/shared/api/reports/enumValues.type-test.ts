import type { components } from "@/shared/api/generated/reports";
import { defineEnumValues } from "./enumValues";

type ExtendedGeneratedReportStatus =
  | components["schemas"]["ReportStatus"]
  | "artificial_new_status";

// Если generated union расширится, неполный transport-регистр обязан сломать tsc.
export const incompleteGeneratedUnion =
  // @ts-expect-error artificial_new_status намеренно не зарегистрирован
  defineEnumValues<ExtendedGeneratedReportStatus>()({
    BACKLOG: "backlog",
    RESOLVED: "resolved",
    FIX: "fix",
    REJECTED: "rejected",
    TEST: "test",
  });
