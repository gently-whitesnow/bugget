import {
  HubConnection,
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel,
} from "@microsoft/signalr";
import { getAppWebSocketUrl } from "@/shared/api";
import { createRetryPolicy } from "./retryPolicy";

export const buildConnection = (): HubConnection => {
  // Строим URL для WebSocket с учетом workspace/team контекста
  // Используем origin страницы чтобы WebSocket шёл через тот же прокси что и HTTP
  const wsPath = getAppWebSocketUrl("/report-page-hub", "v1");
  const fullUrl = `${window.location.origin}${wsPath}`;

  const conn = new HubConnectionBuilder()
    .withUrl(fullUrl, {
      transport: HttpTransportType.WebSockets,
    })
    .withAutomaticReconnect(createRetryPolicy())
    .configureLogging(
      import.meta.env.DEV ? LogLevel.Information : LogLevel.Error
    )
    .build();

  // Должен быть заметно больше server KeepAliveInterval (15s), чтобы избежать ложных timeout.
  conn.serverTimeoutInMilliseconds = 60_000;
  conn.keepAliveIntervalInMilliseconds = 5_000;

  return conn;
};

/**
 * Одна попытка подключения, без внутренних ретраев: повторами занимаются
 * retry-политика SignalR (после успешного старта) и цикл оживления в
 * relations.ts (до него). Вложенный бэк-офф здесь лишь задерживал бы
 * реакцию на возврат пользователя во вкладку.
 */
export const startConnection = async (conn: HubConnection) => {
  await conn.start();
};
