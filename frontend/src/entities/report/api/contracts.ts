import type { components, operations } from "@/shared/api/generated/reports";
import type { Camelized } from "@/shared/lib/types";
import type {
  Attachment,
  BugStep,
  ReportLink,
  ReportLinkDto,
} from "../model/types";

/**
 * Тела запросов и ответов модуля `reports` — выведены из
 * `shared/api/generated/reports.d.ts`, то есть из
 * `specs/contracts/reports/openapi.yaml`.
 *
 * Рукописных DTO здесь больше нет: тело в коде фронта разрешено описывать только
 * `Camelized<T>` над сгенерированной схемой (ADR-0009). Источник правды — yaml:
 * пропало поле в контракте — обращение к нему перестало компилироваться.
 *
 * Query-параметры берутся из `operations[...]` напрямую, без `Camelized`: их
 * camelCase — часть публичного контракта, конверсию они не проходят.
 *
 * Формы, совпадающие с сущностями стора (`Attachment`, `BugStep`, `ReportLink`),
 * выведены в `../model/types` из тех же схем и переиспользуются здесь: у одной
 * схемы контракта — ровно одно представление в коде.
 */

type Schemas = components["schemas"];

/** `POST /v2/reports` — в теле только заголовок. */
export type CreateReportRequest = Camelized<Schemas["ReportCreateRequest"]>;

/** Ответ создания: репорт без вложенного содержимого. */
export type CreateReportResponse = Camelized<Schemas["ReportSummary"]>;

/**
 * Полная карточка репорта — ответ `GET /v2/reports/{aliasId}`.
 * Форма списка живёт отдельно (`ListReportsResponse` в `shared/api/contracts`):
 * LIST не отдаёт `links` и `bugs[].steps`.
 */
export type ReportResponse = Camelized<Schemas["Report"]>;

/** Баг внутри карточки: со вложениями, комментариями и шагами. */
export type BugResponse = Camelized<Schemas["Bug"]>;

/** Публичная форма вложения — одна на весь модуль. */
export type AttachmentResponse = Attachment;

/** Комментарий внутри карточки — вместе с вложениями. */
export type CommentResponse = Camelized<Schemas["Comment"]>;

/**
 * Ответ создания и обновления комментария: `CommentSummary`, без `attachments` —
 * у только что созданного комментария вложений ещё нет.
 */
export type CommentSummaryResponse = Camelized<Schemas["CommentSummary"]>;

/** Шаг воспроизведения — ответ ручек шагов и элемент `bugs[].steps`. */
export type BugStepResponse = BugStep;

/** `PATCH /v2/reports/{aliasId}`: не переданное поле не меняется. */
export type PatchReportRequest = Camelized<Schemas["ReportPatchRequest"]>;

/** Что изменилось в репорте после PATCH. `id` — alias, как и в URL. */
export type PatchReportResponse = Camelized<Schemas["ReportPatchResult"]>;

/** Координаты репорта для редиректа со старой ссылки. */
export type LegacyReportResolveResponse = Camelized<
  Schemas["LegacyReportResolve"]
>;

/** `POST /v2/reports/{aliasId}/bugs`: полноту пары receive/expect проверяет сервер. */
export type CreateBugRequest = Camelized<Schemas["BugRequest"]>;

/** `PATCH .../bugs/{bugId}`: не переданное поле не меняется. */
export type PatchBugRequest = Camelized<Schemas["BugPatchRequest"]>;

/** Ответ создания бага: без вложенных коллекций и без `reportId`. */
export type CreateBugResponse = Camelized<Schemas["BugSummary"]>;

/** Что изменилось в баге после PATCH. */
export type PatchBugResponse = Camelized<Schemas["BugPatchResult"]>;

/** Тело создания и обновления шага. */
export type BugStepRequest = Camelized<Schemas["BugStepRequest"]>;

/** Полный список шагов бага в нужном порядке. */
export type BugStepOrderRequest = Camelized<Schemas["BugStepsOrderRequest"]>;

/** Тело создания комментария: текст и (опционально) аудитория. */
export type CreateCommentRequest = Camelized<Schemas["CommentRequest"]>;

/** Тело обновления комментария — та же схема, что и у создания. */
export type UpdateCommentRequest = Camelized<Schemas["CommentRequest"]>;

/** Новое имя вложения. */
export type AttachmentRenameRequest = Camelized<
  Schemas["AttachmentRenameRequest"]
>;

/**
 * Загружаемый файл. Тело multipart конверсию регистра не проходит, поэтому
 * имя поля берётся из схемы как есть, без `Camelized`.
 */
export type AttachmentUploadForm = Schemas["AttachmentUpload"];

/** Тело создания и обновления ссылки репорта. */
export type ReportLinkRequest = ReportLinkDto;

/** Ссылка репорта в ответе. */
export type ReportLinkResponse = ReportLink;

/** Query загрузки вложения бага: `attachType`. */
export type UploadAttachmentQuery = NonNullable<
  operations["BugAttachments_CreateBugAttachment"]["parameters"]["query"]
>;
