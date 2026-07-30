import type { components } from "@/shared/api/generated/reports";
import type { Camelized } from "@/shared/lib/types";

/**
 * Вложение в том виде, в котором его отдаёт контракт модуля `reports`
 * (`AttachmentSummary`): и внутри репорта, и в ответах ручек загрузки и
 * переименования форма одна.
 *
 * Форма выведена из контракта, а не описана руками: рукописный DTO — второе
 * представление тела, которое расходится с yaml молча (ADR-0009). `Camelized`
 * учитывает конверсию регистра в интерсепторе (`shared/api/instances/base.ts`):
 * провод — snake_case, код фронта — camelCase.
 *
 * `attachType` здесь `number`, как в контракте; значения перечислены в
 * `AttachmentTypes` (`shared/config`) и совпадают с ним 0..3.
 */
export type Attachment = Camelized<components["schemas"]["AttachmentSummary"]>;
