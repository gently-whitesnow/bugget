import type { reportsApi } from "@/shared/api";
import type {
  Attachment,
  BugStep,
  ReportLink,
  ReportLinkDto,
} from "../model/types";

/**
 * Имена форм модуля `reports`, которыми пользуется страница репорта.
 *
 * Все они — типы операций из `shared/api/reports`, то есть выведены из
 * `specs/contracts/reports/openapi.yaml` вместе с путём и методом: сменилась
 * схема ответа у операции — здесь перестало компилироваться. Рукописных DTO и
 * алиасов на «просто схему», не привязанную к операции, тут нет (ADR-0009).
 *
 * Формы, совпадающие с сущностями стора (`Attachment`, `BugStep`, `ReportLink`),
 * выведены в `../model/types` из тех же операций и переиспользуются здесь: у
 * одной формы контракта — одно представление в коде.
 */

/** `POST /v2/reports` — в теле только заголовок. */
export type CreateReportRequest = reportsApi.CreateReportBody;

/** Ответ создания: репорт без вложенного содержимого. */
export type CreateReportResponse = reportsApi.CreateReportResult;

/**
 * Полная карточка репорта — ответ `GET /v2/reports/{aliasId}`.
 * Форма списка живёт отдельно: LIST не отдаёт `links` и `bugs[].steps`.
 */
export type ReportResponse = reportsApi.ReportResult;

/** Баг внутри карточки: со вложениями, комментариями и шагами. */
export type BugResponse = NonNullable<ReportResponse["bugs"]>[number];

/** Публичная форма вложения — одна на весь модуль. */
export type AttachmentResponse = Attachment;

/** Комментарий внутри карточки — вместе с вложениями. */
export type CommentResponse = NonNullable<BugResponse["comments"]>[number];

/**
 * Ответ создания и обновления комментария: без `attachments` — у только что
 * созданного комментария вложений ещё нет.
 */
export type CommentSummaryResponse = reportsApi.CommentResult;

/** Шаг воспроизведения — ответ ручек шагов и элемент `bugs[].steps`. */
export type BugStepResponse = BugStep;

/** `PATCH /v2/reports/{aliasId}`: не переданное поле не меняется. */
export type PatchReportRequest = reportsApi.PatchReportBody;

/** Что изменилось в репорте после PATCH. `id` — alias, как и в URL. */
export type PatchReportResponse = reportsApi.PatchReportResult;

/** Координаты репорта для редиректа со старой ссылки. */
export type LegacyReportResolveResponse = reportsApi.LegacyReportResolveResult;

/** `POST /v2/reports/{aliasId}/bugs`: полноту пары receive/expect проверяет сервер. */
export type CreateBugRequest = reportsApi.CreateBugBody;

/** `PATCH .../bugs/{bugId}`: не переданное поле не меняется. */
export type PatchBugRequest = reportsApi.PatchBugBody;

/** Ответ создания бага: без вложенных коллекций и без `reportId`. */
export type CreateBugResponse = reportsApi.CreateBugResult;

/** Что изменилось в баге после PATCH. */
export type PatchBugResponse = reportsApi.PatchBugResult;

/** Тело создания и обновления шага. */
export type BugStepRequest = reportsApi.BugStepBody;

/** Полный список шагов бага в нужном порядке. */
export type BugStepOrderRequest = reportsApi.BugStepsOrderBody;

/** Тело создания комментария: текст и (опционально) аудитория. */
export type CreateCommentRequest = reportsApi.CommentBody;

/** Тело обновления комментария — та же схема, что и у создания. */
export type UpdateCommentRequest = reportsApi.CommentBody;

/** Тело создания и обновления ссылки репорта. */
export type ReportLinkRequest = ReportLinkDto;

/** Ссылка репорта в ответе. */
export type ReportLinkResponse = ReportLink;

/** Страница списка репортов и query её операции. */
export type ListReportsResponse = reportsApi.ListReportsResult;
export type ListReportsQuery = reportsApi.ListReportsQuery;
