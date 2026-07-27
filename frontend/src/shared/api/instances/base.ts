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
 * URL-паттерны, для которых wire-формат остаётся snake_case в обе стороны.
 * Используется для эндпоинтов, чьи типы сгенерированы из OpenAPI YAML
 * (см. shared/api/generated/*.d.ts) — там DTO-поля совпадают с протоколом.
 */
const skipCaseConversionPatterns: RegExp[] = [
  /\/v2\/analytics(\/|$|\?)/,
  /\/v2\/reports\/\d+\/analytics(\/|$|\?)/,
];

const shouldSkipCaseConversion = (url: string | undefined): boolean => {
  if (!url) return false;
  return skipCaseConversionPatterns.some((re) => re.test(url));
};

/**
 * RFC 9457 Problem Details. Все имена RFC однословные, конверсия их не изменила бы,
 * но в `errors` лежит словарь «имя поля → ошибки», и рекурсивная конверсия ключей
 * переписала бы имена полей формы. Поэтому problem+json не конвертируем вовсе.
 */
const isProblemDetailsResponse = (headers: unknown): boolean => {
  const contentType = (headers as Record<string, unknown> | undefined)?.[
    "content-type"
  ];
  return (
    typeof contentType === "string" &&
    contentType.toLowerCase().includes("application/problem+json")
  );
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
      return Promise.reject(error);
    }
  );

  axiosInstance.interceptors.response.use((response) => {
    if (
      response.data &&
      !shouldSkipCaseConversion(response.config?.url) &&
      !isProblemDetailsResponse(response.headers)
    ) {
      response.data = convertObjectToCamel(response.data);
    }
    return response;
  });
};

const setupRequestInterceptors = (axiosInstance: AxiosInstance) => {
  axiosInstance.interceptors.request.use((config) => {
    if (
      config.headers["Content-Type"] !== "multipart/form-data" &&
      config.data &&
      !shouldSkipCaseConversion(config.url)
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
