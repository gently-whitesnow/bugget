import { describe, expect, it } from "vitest";
import type { components } from "@/shared/api/generated/reports";
import type { Camelized } from "@/shared/lib/types";
import type { AttachmentResponse } from "./contracts";

/**
 * Карточка репорта (`GET /v2/reports/{aliasId}`) пока читается рукописным DTO —
 * перевод на сгенерированный `Report` идёт отдельным слайсом. Пока так, форма
 * вложения в этом DTO обязана совпадать с публичной формой контракта: сужение
 * MAIN-63 убрало с провода `storage_key`, `storage_kind`, `length_bytes`,
 * `mime_type` и `is_gzip_compressed`, и рукописный тип не должен их вернуть —
 * ни объявлением мёртвого поля, ни потерей живого.
 */

type WireAttachment = components["schemas"]["AttachmentSummary"];

/** Строгое равенство типов: при расхождении `false` не присвоится `true`. */
type Equal<A, B> =
  (<T>() => T extends A ? 1 : 2) extends <T>() => T extends B ? 1 : 2
    ? true
    : false;

const cardAttachmentMatchesWire: Equal<
  keyof Camelized<WireAttachment>,
  keyof AttachmentResponse
> = true;

describe("DTO карточки репорта", () => {
  it("описывает ровно публичную форму вложения из контракта", () => {
    // Равенство держит `tsc --noEmit` (гейт frontend-typecheck); тест фиксирует намерение.
    expect(cardAttachmentMatchesWire).toBe(true);
  });
});
