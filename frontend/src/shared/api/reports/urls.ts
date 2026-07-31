import type { paths } from "@/shared/api/generated/reports";
import { buildOperationPath } from "@/shared/api/operation";
import { buildFullApiUrl } from "@/shared/lib/buildFullUrl";

/* ── Адреса, которые нужны строкой ─────────────────────────────────────────── */

/**
 * Содержимое вложения запрашивает браузер — оно уезжает в `src` картинки, в
 * `href` ссылки и в `fetch` за бинарным телом, а не в axios. Поэтому здесь нужен
 * адрес строкой, и шаблон его берётся из контракта, как у аватара в модуле
 * `users`: иначе адрес картинки расходится с адресом ручки молча.
 *
 * Оригинал и превью — шесть отдельных путей контракта, а не один путь с
 * дописанным суффиксом. Каждый объявлен литералом и проверен как `keyof paths`:
 * переименование любого из них в `specs/contracts/reports/openapi.yaml` ломает
 * компиляцию здесь, а не молча ведёт картинку в 404. Строковая конкатенация
 * `/preview` этого бы не дала — `buildOperationPath` принимает произвольную
 * строку, и склеенный путь остался бы зелёным для `tsc`.
 */

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

/** Шесть путей контракта, по которым браузер забирает содержимое вложения. */
export type AttachmentContentRoute = ContentRoutes[keyof ContentRoutes][
  | "original"
  | "preview"];

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

  // Единственное, что к пути контракта добавляется, — хвостовой слэш у
  // оригинала: рукописный адрес заканчивался сегментом после `content/`, и у
  // оригинала этот сегмент был пустым. Маршрут ASP.NET отвечает одинаково и без
  // слэша, но публичные URL в этой программе работ не меняются. Сам путь при
  // этом остаётся тем, что объявлен в контракте.
  return preview ? path : `${path}/`;
};

/**
 * Полный адрес для браузера: тот же путь плюс префикс рабочего пространства и
 * команды, который для axios дописывает интерсептор `instances/app.ts`.
 */
export const attachmentContentUrl = (target: AttachmentContentTarget): string =>
  buildFullApiUrl(attachmentContentPath(target));
