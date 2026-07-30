import type { components } from "@/shared/api/generated/reports";
import type { Camelized } from "@/shared/lib/types";

/**
 * Комментарий к багу — форма из контракта модуля `reports` (`Comment`),
 * выведенная из yaml, а не описанная руками (ADR-0009).
 *
 * `attachments` — ключ обязательный, значение допускает `null`: контракт
 * различает «вложений нет» (пустой массив) и «не запрашивались» (`null`).
 */
export type Comment = Camelized<components["schemas"]["Comment"]>;
