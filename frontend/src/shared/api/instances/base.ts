import axios, { AxiosInstance } from "axios";
import {
  convertObjectToCamel,
  convertObjectToSnake,
} from "@/shared/lib/convertCases";
import { buildAuthRedirectUrl, getAuthEntryPath } from "@/shared/lib/auth";

let signalRConnectionId: string | null = null;

export const setSignalRConnectionId = (id: string | null) => {
  signalRConnectionId = id;
};

export const getSignalRConnectionId = () => signalRConnectionId;

/**
 * Граница wire↔UI одна на все HTTP-модули и не зависит от URL:
 *
 *   * JSON-тело ответа (успех и ошибка одинаково) → camelCase;
 *   * JSON-тело запроса → snake_case;
 *   * multipart, query и path не преобразуются — там имена и так camelCase и
 *     принадлежат публичному контракту (ADR-0009);
 *   * `application/problem+json` не преобразуется вовсе — см. ниже.
 *
 * URL-исключений из конверсии здесь нет намеренно: они делали форму данных
 * функцией адреса, из-за чего один и тот же тип читался по-разному в разных
 * модулях. Сгенерированные типы описывают провод, camelCase-форма выводится из
 * них type-only мостом `Camelized<T>` (`shared/lib/types/camelize.ts`).
 */

/**
 * RFC 9457 Problem Details. Все имена RFC однословные, конверсия их не изменила бы,
 * но в `errors` лежит словарь «имя поля → ошибки», и рекурсивная конверсия ключей
 * переписала бы имена полей формы. Поэтому problem+json не конвертируем вовсе.
 */
const contentTypeFrom = (headers: unknown): unknown => {
  const get = (headers as { get?: unknown } | undefined)?.get;
  if (typeof get === "function") return get.call(headers, "content-type");
  return Object.entries(
    (headers as Record<string, unknown> | undefined) ?? {}
  ).find(([name]) => name.toLowerCase() === "content-type")?.[1];
};

const isProblemDetailsResponse = (headers: unknown): boolean => {
  const contentType = contentTypeFrom(headers);
  return (
    typeof contentType === "string" &&
    contentType.toLowerCase().includes("application/problem+json")
  );
};

const isJsonResponse = (headers: unknown): boolean => {
  const contentType = contentTypeFrom(headers);
  return (
    typeof contentType === "string" &&
    contentType.toLowerCase().includes("application/json")
  );
};

/**
 * Конвертируем только тела, которые действительно JSON, — и на успешном пути, и на
 * пути ошибки одинаково. Бинарный ответ (вложение) — не набор именованных полей:
 * рекурсивный обход превратил бы его в пустой объект.
 *
 * Ответ без `Content-Type` конвертируется: так отвечают промежуточные узлы, тела у
 * них либо JSON, либо строка, а строку обход возвращает как есть. Это осознанный
 * fallback, а не недосмотр: он сохраняет поведение, на которое рассчитывает фронт.
 */
const shouldConvertBody = (headers: unknown): boolean => {
  if (isProblemDetailsResponse(headers)) return false;
  if (contentTypeFrom(headers) === undefined) return true;
  return isJsonResponse(headers);
};

const setupResponseInterceptors = (axiosInstance: AxiosInstance) => {
  axiosInstance.interceptors.response.use(
    (response) => response,
    (error) => {
      // Nginx возвращает 401 JSON для API-запросов без auth.
      // Навигируем браузер на страницу логина, сохраняя текущий путь в next.
      // Self-hosted -> /login, SaaS -> / (лендинг с логином).
      // Anon не использует login-flow, поэтому 401 просто пробрасываем выше.
      if (error?.response?.status === 401) {
        const loginPath = getAuthEntryPath();
        if (loginPath && window.location.pathname !== loginPath) {
          const next = window.location.pathname + window.location.search;
          const redirectUrl = buildAuthRedirectUrl(next);
          if (!redirectUrl) {
            return Promise.reject(error);
          }
          window.location.replace(redirectUrl);
          return new Promise(() => {}); // pending forever — страница перезагрузится
        }
      }

      if (error && error.response?.status >= 500) {
        const issueName = `${
          error.response.status
        } ${error.config?.method?.toUpperCase()} ${error.config?.url}`.trim();
        console.error(issueName);
      }

      if (error?.response?.data && shouldConvertBody(error.response.headers)) {
        error.response.data = convertObjectToCamel(error.response.data);
      }
      return Promise.reject(error);
    }
  );

  axiosInstance.interceptors.response.use((response) => {
    if (response.data && shouldConvertBody(response.headers)) {
      response.data = convertObjectToCamel(response.data);
    }
    return response;
  });
};

const setupRequestInterceptors = (axiosInstance: AxiosInstance) => {
  axiosInstance.interceptors.request.use((config) => {
    if (
      config.headers["Content-Type"] !== "multipart/form-data" &&
      config.data
    ) {
      config.data = convertObjectToSnake(config.data);
    }

    if (signalRConnectionId) {
      config.headers["X-Signal-R-Connection-Id"] = signalRConnectionId;
    }

    return config;
  });
};

export const createApiInstance = (timeout = 10000): AxiosInstance => {
  const instance = axios.create({
    baseURL: "",
    timeout,
  });

  setupResponseInterceptors(instance);
  setupRequestInterceptors(instance);

  return instance;
};
