// Единственный разбор тела HTTP-ошибки (ADR-0008). Понимает Problem Details,
// legacy `{error, reason}` (на время раскатки) и не-JSON/пустое тело (401 от
// nginx). Ничего не бросает: возвращает только доказанные поля.

export interface ApiError {
  status?: number;
  /** `code` из Problem Details или `error` из legacy; по нему ветвится UI. */
  code?: string;
  detail?: string;
  title?: string;
  /** Готовый текст для тоста: `detail`, иначе безопасный `title`. */
  message?: string;
}

const BUGGET_ERROR_TYPE_PREFIX = "urn:bugget:error:";

const asRecord = (value: unknown): Record<string, unknown> | undefined =>
  typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : undefined;

const asFilledString = (value: unknown): string | undefined => {
  if (typeof value !== "string") return undefined;
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : undefined;
};

const toSafeTitle = (
  title: string | undefined,
  type: string | undefined
): string | undefined => {
  if (!title) return undefined;
  // Стандартный Problem Details ASP.NET несёт не наш URI type и технический title.
  return !type || type.startsWith(BUGGET_ERROR_TYPE_PREFIX) ? title : undefined;
};

// Если ответ потерял `code`, хвост `urn:bugget:error:<code>` — единственный код.
const codeFromType = (type: string | undefined): string | undefined => {
  return type?.startsWith(BUGGET_ERROR_TYPE_PREFIX)
    ? asFilledString(type.slice(BUGGET_ERROR_TYPE_PREFIX.length))
    : undefined;
};

export const parseApiError = (error: unknown): ApiError => {
  const response = asRecord(asRecord(error)?.response);
  const status =
    typeof response?.status === "number" ? response.status : undefined;

  // Не-JSON (nginx отдаёт html/текст) и пустое тело: полей нет, статус есть.
  const body = asRecord(response?.data);
  if (!body) return { status };

  const code =
    asFilledString(body.code) ??
    asFilledString(body.error) ??
    codeFromType(asFilledString(body.type));
  const detail = asFilledString(body.detail) ?? asFilledString(body.reason);
  const title = toSafeTitle(
    asFilledString(body.title),
    asFilledString(body.type)
  );

  return { status, code, detail, title, message: detail ?? title };
};
