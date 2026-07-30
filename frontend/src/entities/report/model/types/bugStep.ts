import type { components } from "@/shared/api/generated/reports";
import type { Camelized } from "@/shared/lib/types";

/**
 * Шаг воспроизведения — форма из контракта модуля `reports` (`BugStep`),
 * выведенная из yaml, а не описанная руками (ADR-0009).
 */
export type BugStep = Camelized<components["schemas"]["BugStep"]>;
