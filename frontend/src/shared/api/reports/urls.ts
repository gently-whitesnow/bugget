import { buildOperationPath } from "@/shared/api/operation";
import { buildFullApiUrl } from "@/shared/lib/buildFullUrl";

/* ── Адреса, которые нужны строкой ─────────────────────────────────────────── */

/**
 * Содержимое вложения запрашивает браузер — оно уезжает в `src` картинки, в
 * `href` ссылки и в `fetch` за бинарным телом, а не в axios. Поэтому здесь нужен
 * адрес строкой, и шаблон его берётся из контракта, как у аватара в модуле
 * `users`: иначе адрес картинки расходится с адресом ручки молча.
 */

const BUG_CONTENT =
  "/v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}/content";
const COMMENT_CONTENT =
  "/v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments/{id}/content";
const STEP_CONTENT =
  "/v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments/{id}/content";

export type AttachmentContentTarget = {
  /** Alias репорта — тот же сегмент, что и у операций модуля. */
  reportId: string;
  bugId: number;
  id: number;
  /** Вложение комментария: у шага и у самого бага сегмента нет. */
  commentId?: number;
  /** Вложение шага воспроизведения. */
  stepId?: number;
  /** Превью вместо оригинала. */
  preview?: boolean;
};

/** Путь ручки без префикса контекста — в том виде, в каком он записан в контракте. */
export const attachmentContentPath = ({
  reportId,
  bugId,
  id,
  commentId,
  stepId,
  preview,
}: AttachmentContentTarget): string => {
  const template = commentId
    ? COMMENT_CONTENT
    : stepId
      ? STEP_CONTENT
      : BUG_CONTENT;

  const path = buildOperationPath(template, {
    aliasId: reportId,
    bugId,
    id,
    commentId,
    stepId,
  });

  // Провод сохраняется дословно: рукописный адрес всегда заканчивался сегментом
  // после `content/` — либо `preview`, либо пустой строкой, то есть хвостовым
  // слэшем. Маршрут ASP.NET отвечает одинаково и без него, но публичные URL в
  // этой программе работ не меняются.
  return preview ? `${path}/preview` : `${path}/`;
};

/**
 * Полный адрес для браузера: тот же путь плюс префикс рабочего пространства и
 * команды, который для axios дописывает интерсептор `instances/app.ts`.
 */
export const attachmentContentUrl = (target: AttachmentContentTarget): string =>
  buildFullApiUrl(attachmentContentPath(target));
