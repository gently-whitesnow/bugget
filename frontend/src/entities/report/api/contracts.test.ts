import { describe, expect, it } from "vitest";
import type { components } from "@/shared/api/generated/reports";
import type { Camelized } from "@/shared/lib/types";
import type { BugClientEntity } from "../model/types";
import type {
  AttachmentResponse,
  BugResponse,
  CommentResponse,
  CommentSummaryResponse,
  ReportResponse,
} from "./contracts";

/**
 * Формы выведены из контракта; проверяются сужения фронта поверх него — ни одно
 * не должно молча съесть живое поле. Равенства держит `tsc --noEmit`.
 */

type Schemas = components["schemas"];

/** Строгое равенство типов: при расхождении `false` не присвоится `true`. */
type Equal<A, B> =
  (<T>() => T extends A ? 1 : 2) extends <T>() => T extends B ? 1 : 2
    ? true
    : false;

const cardIsWireReport: Equal<
  ReportResponse,
  Camelized<Schemas["Report"]>
> = true;

const bugIsWireBug: Equal<BugResponse, Camelized<Schemas["Bug"]>> = true;

const attachmentIsWireSummary: Equal<
  AttachmentResponse,
  Camelized<Schemas["AttachmentSummary"]>
> = true;

const commentIsWireComment: Equal<
  CommentResponse,
  Camelized<Schemas["Comment"]>
> = true;

/** `CommentSummary` — это `Comment` без `attachments`. */
const commentSummaryHasNoAttachments: Equal<
  keyof CommentSummaryResponse,
  Exclude<keyof CommentResponse, "attachments">
> = true;

// Баг стора — баг провода без `steps` (у шагов свой стор) плюс клиентские поля.
// `reportId` здесь alias репорта, а не числовой `report_id` провода.
const storeBugKeepsEveryWireField: Equal<
  keyof BugClientEntity,
  Exclude<keyof BugResponse, "steps"> | "clientId" | "isLocalOnly"
> = true;

const storeBugReportIdIsAlias: Equal<BugClientEntity["reportId"], string> =
  true;
const wireBugReportIdIsNumber: Equal<BugResponse["reportId"], number> = true;

describe("контракт модуля reports на фронте", () => {
  it("карточка, баг, вложение и комментарий описаны формой контракта", () => {
    expect(cardIsWireReport).toBe(true);
    expect(bugIsWireBug).toBe(true);
    expect(attachmentIsWireSummary).toBe(true);
    expect(commentIsWireComment).toBe(true);
  });

  it("ответ создания комментария — тот же комментарий без вложений", () => {
    expect(commentSummaryHasNoAttachments).toBe(true);
  });

  it("баг в сторе не теряет полей провода, кроме шагов", () => {
    expect(storeBugKeepsEveryWireField).toBe(true);
  });

  it("reportId в сторе — alias, на проводе — число; их не путают", () => {
    expect(storeBugReportIdIsAlias).toBe(true);
    expect(wireBugReportIdIsNumber).toBe(true);
  });
});
