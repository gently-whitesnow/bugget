import type { paths } from "@/shared/api/generated/reports";
import { buildOperationPath } from "@/shared/api/operation";
import { buildFullApiUrl } from "@/shared/lib/buildFullUrl";

// Содержимое вложения забирает браузер (`src`, `href`, `fetch`), а не axios,
// поэтому адрес нужен строкой. Все шесть путей — литералы `keyof paths`, а не
// конкатенация `/preview`: переименование в контракте ломает компиляцию.

export const ATTACHMENT_CONTENT = {
  bug: {
    original: "/v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}/content",
    preview:
      "/v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}/content/preview",
  },
  comment: {
    original:
      "/v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments/{id}/content",
    preview:
      "/v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments/{id}/content/preview",
  },
  step: {
    original:
      "/v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments/{id}/content",
    preview:
      "/v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments/{id}/content/preview",
  },
} as const satisfies Record<
  string,
  Record<"original" | "preview", keyof paths>
>;

type ContentRoutes = typeof ATTACHMENT_CONTENT;

export type AttachmentContentRoute = ContentRoutes[keyof ContentRoutes][
  | "original"
  | "preview"];

export type AttachmentContentTarget = {
  reportId: string;
  bugId: number;
  id: number;
  commentId?: number;
  stepId?: number;
  preview?: boolean;
};

/** Путь ручки без префикса контекста — как в контракте. */
export const attachmentContentPath = ({
  reportId,
  bugId,
  id,
  commentId,
  stepId,
  preview,
}: AttachmentContentTarget): string => {
  const owner = commentId
    ? ATTACHMENT_CONTENT.comment
    : stepId
      ? ATTACHMENT_CONTENT.step
      : ATTACHMENT_CONTENT.bug;

  const path = buildOperationPath(preview ? owner.preview : owner.original, {
    aliasId: reportId,
    bugId,
    id,
    commentId,
    stepId,
  });

  // Хвостовой слэш у оригинала — наследие рукописного адреса: публичные URL
  // не меняются, хотя маршрут ASP.NET отвечает и без него.
  return preview ? path : `${path}/`;
};

/** Полный адрес: путь плюс префикс контекста, как у интерсептора appApi. */
export const attachmentContentUrl = (target: AttachmentContentTarget): string =>
  buildFullApiUrl(attachmentContentPath(target));
