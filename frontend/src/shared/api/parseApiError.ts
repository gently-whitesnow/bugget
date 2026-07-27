/**
 * Единственная точка разбора тела HTTP-ошибки на фронте.
 *
 * Backend мигрирует на RFC 9457 Problem Details (решение в MAIN-64), но делает это
 * слайсами, поэтому парсер обязан одинаково понимать три формы одновременно:
 *
 * 1. Problem Details — `{type, title, status, detail, instance, code}`,
 *    `Content-Type: application/problem+json`;
 * 2. legacy — `{error, reason}` (сегодняшний провод всех 4xx/5xx);
 * 3. не-JSON или пустое тело — в частности 401 от внешнего nginx, который в этом
 *    монорепозитории остаётся документированным исключением.
 *
 * Парсер ничего не бросает: на любой вход он возвращает `ApiError` с теми полями,
 * которые удалось доказать.
 */

/** Разобранная ошибка API в терминах, независимых от формы провода. */
export interface ApiError {
  /** HTTP-статус ответа, если он вообще был (у сетевой ошибки его нет). */
  status?: number;
  /**
   * Стабильный машинный код ошибки: `code` из Problem Details,
   * `error` из legacy-формы. По нему ветвится UI.
   */
  code?: string;
  /** Человекочитаемая причина: `detail` из Problem Details, `reason` из legacy. */
  detail?: string;
  /** Заголовок класса ошибки из Problem Details, безопасный для показа (см. ниже). */
  title?: string;
  /** Готовый текст для тоста: `detail`, иначе безопасный `title`. */
  message?: string;
}

/**
 * Стандартные reason phrase из HTTP: ASP.NET подставляет их в `title` дефолтной
 * фабрики Problem Details. Английская техническая строка вида «Not Found» в русском
 * тосте хуже, чем статический текст-заглушка, поэтому такой `title` считаем
 * небезопасным для показа и отбрасываем.
 */
const HTTP_REASON_PHRASES: Record<number, string> = {
  400: "bad request",
  401: "unauthorized",
  403: "forbidden",
  404: "not found",
  409: "conflict",
  415: "unsupported media type",
  422: "unprocessable entity",
  429: "too many requests",
  500: "internal server error",
  502: "bad gateway",
  503: "service unavailable",
  504: "gateway timeout",
};

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
  status: number | undefined
): string | undefined => {
  if (!title) return undefined;
  const phrase = status === undefined ? undefined : HTTP_REASON_PHRASES[status];
  return phrase && title.toLowerCase() === phrase ? undefined : title;
};

/**
 * `type` в Problem Details — URI вида `urn:bugget:error:<code>`. `code` обязателен,
 * но если конкретный ответ его потерял, хвост `type` — единственный машинный код,
 * который у нас есть.
 */
const codeFromType = (type: string | undefined): string | undefined => {
  if (!type) return undefined;
  const tail = type.split(/[:/]/).pop();
  return asFilledString(tail);
};

/** Разбирает ошибку axios (или что угодно другое) в `ApiError`. */
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
  const title = toSafeTitle(asFilledString(body.title), status);

  return { status, code, detail, title, message: detail ?? title };
};
