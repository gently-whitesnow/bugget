import type { AttachmentWire } from "./wire";

/**
 * Выведено из операции, а не описано руками (ADR-0009). `attachType` — `number`,
 * как в контракте; значения — `AttachmentTypes` (`shared/config`), 0..3.
 */
export type Attachment = AttachmentWire;
