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

// Граница wire↔UI одна на все HTTP-модули и не зависит от URL (ADR-0009): JSON
// ответа → camelCase, запроса → snake_case; multipart, query и path — как есть.

// RFC 9457 problem+json не конвертируем: в `errors` ключи — имена полей формы.
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

// Конвертируем только JSON: бинарное тело обход превратил бы в пустой объект.
// Ответ без `Content-Type` конвертируется осознанно — так отвечают прокси.
const shouldConvertBody = (headers: unknown): boolean => {
  if (isProblemDetailsResponse(headers)) return false;
  if (contentTypeFrom(headers) === undefined) return true;
  return isJsonResponse(headers);
};

const setupResponseInterceptors = (axiosInstance: AxiosInstance) => {
  axiosInstance.interceptors.response.use(
    (response) => response,
    (error) => {
      // 401 от nginx: уводим на страницу логина (self-hosted /login, SaaS /),
      // сохраняя путь в next. Anon login-flow не использует — 401 идёт выше.
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
