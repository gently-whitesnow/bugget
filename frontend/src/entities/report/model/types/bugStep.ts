import type { BugStepWire } from "./wire";

/**
 * Шаг воспроизведения — форма из контракта модуля `reports`, выведенная из
 * операции карточки репорта, а не описанная руками (ADR-0009).
 */
export type BugStep = BugStepWire;
