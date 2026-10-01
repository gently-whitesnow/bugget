import { createDomain } from "effector";
import { buildConnection, startConnection } from "./connection";
import { HubConnection, HubConnectionState } from "@microsoft/signalr";
import { customParsers, SocketEvent, SocketPayload } from "./contracts";
import { setSignalRConnectionId } from "@/shared/api";

type ConnectionReady = HubConnection & { started: true };

export enum SocketConnectionStatus {
  IDLE = "idle",
  CONNECTED = "connected",
  DISCONNECTED = "disconnected",
}

export const reconnectStuckTimeoutMs = 120_000;

/** Сколько ждём stop() зомби-сокета, прежде чем бросить его и строить новый */
export const stopTimeoutMs = 3_000;

const socket = createDomain();

// Поколение сокета растёт с каждой попыткой подключения; колбэки устаревшей
// попытки игнорируются, поэтому новая никогда не ждёт зависшую старую.
let socketGeneration = 0;

/** Мёртвый транспорт может не ответить никогда — ждём не дольше timeoutMs */
const waitAtMost = (createPromise: () => Promise<unknown>, timeoutMs: number) =>
  new Promise<void>((resolve) => {
    const timeoutId = setTimeout(resolve, timeoutMs);

    Promise.resolve()
      .then(createPromise)
      .catch(() => undefined)
      .then(() => {
        clearTimeout(timeoutId);
        resolve();
      });
  });

export const connectionStarted = socket.createEvent<ConnectionReady>();
export const connectionClosed = socket.createEvent<Error | undefined>();
export const connectionReconnecting = socket.createEvent<Error | undefined>();
export const connectionReconnected = socket.createEvent<string | null>();

/** Вкладка видима, окно в фокусе или страница из bfcache — лечатся одинаково */
export const appWokeUp = socket.createEvent();

/** Машина спала: соединение могло остаться Connected, но уже не работает */
export const longSleepDetected = socket.createEvent();
export const browserWentOnline = socket.createEvent();
export const browserWentOffline = socket.createEvent();
export const connectionRecoveryStarted = socket.createEvent();
export const reconnectStuckDetected = socket.createEvent();

/** Связь восстановлена после разрыва: пропущенные события надо перезабрать */
export const connectionRestored = socket.createEvent();

export const socketEventReceived = socket.createEvent<{
  type: SocketEvent;
  payload: SocketPayload[SocketEvent];
}>();

export const watchSocketEvents = (
  listener: (evt: {
    type: SocketEvent;
    payload: SocketPayload[SocketEvent];
  }) => void
) => {
  return socketEventReceived.watch(listener);
};

export const joinReportFx = socket.createEffect(
  async ({ conn, reportId }: { conn: HubConnection; reportId: string }) => {
    await conn.invoke("JoinReportGroupAsync", reportId);
  }
);

export const leaveReportFx = socket.createEffect(
  async ({ conn, reportId }: { conn: HubConnection; reportId: string }) => {
    await conn.invoke("LeaveReportGroupAsync", reportId);
  }
);

export const $connection = socket
  .createStore<ConnectionReady | null>(null)
  .on(connectionStarted, (_, conn) => {
    setSignalRConnectionId(conn.connectionId ?? null);
    return conn;
  })
  .reset(connectionClosed);

export const $socketConnectionStatus = socket
  .createStore<SocketConnectionStatus>(SocketConnectionStatus.IDLE)
  .on(connectionStarted, () => SocketConnectionStatus.CONNECTED)
  .on(connectionReconnected, () => SocketConnectionStatus.CONNECTED)
  .on(connectionReconnecting, () => SocketConnectionStatus.DISCONNECTED)
  .on(connectionClosed, () => SocketConnectionStatus.DISCONNECTED);
export const $isConnected = $socketConnectionStatus.map(
  (status) => status === SocketConnectionStatus.CONNECTED
);

/** Меняется при каждом реконнекте — членство в группах хаба теряется */
export const $connectionId = socket
  .createStore<string | null>(null)
  .on(connectionStarted, (_, conn) => conn.connectionId)
  .on(connectionReconnected, (_, connectionId) => connectionId)
  .reset(connectionClosed);

// вне браузера (тесты/SSR) navigator.onLine нет — считаем, что сеть есть
export const $isOnline = socket
  .createStore(globalThis.navigator?.onLine ?? true)
  .on(browserWentOnline, () => true)
  .on(browserWentOffline, () => false);

export const waitBeforeRevivalFx = socket.createEffect(
  (delayMs: number) =>
    new Promise<void>((resolve) => setTimeout(resolve, delayMs))
);

export const waitForReconnectStuckFx = socket.createEffect(
  (recoveryId: number) =>
    new Promise<number>((resolve) =>
      setTimeout(() => resolve(recoveryId), reconnectStuckTimeoutMs)
    )
);

/** Обнуляется удачным стартом: дальше обрывами занимается retry SignalR */
export const $revivalAttempts = socket
  .createStore(0)
  .on(waitBeforeRevivalFx, (count) => count + 1)
  .reset(connectionStarted);

export const $wasDisconnected = socket
  .createStore(false)
  .on(connectionClosed, () => true)
  .reset(connectionRestored);

export const $recoveryId = socket
  .createStore(0)
  .on(connectionRecoveryStarted, (id) => id + 1);

export const $isRecoveryInProgress = socket
  .createStore(false)
  .on(connectionRecoveryStarted, () => true)
  .reset(connectionStarted)
  .reset(connectionReconnected);

export const $reconnectStuck = socket
  .createStore(false)
  .on(reconnectStuckDetected, () => true)
  .reset(connectionStarted)
  .reset(connectionReconnected);

export const initSocketFx = socket.createEffect(async () => {
  const currentConn = $connection.getState();
  if (currentConn && currentConn.state !== HubConnectionState.Disconnected) {
    return;
  }

  const generation = ++socketGeneration;
  const isStale = () => generation !== socketGeneration;

  const conn = buildConnection();

  const handlers = new Map<SocketEvent, (p: unknown) => void>();

  Object.values(SocketEvent).forEach((event) => {
    const customParser = customParsers[event];

    const handler = (...args: unknown[]) => {
      if (isStale()) return;

      let payload: SocketPayload[SocketEvent];

      if (customParser) {
        payload = customParser(...args) as SocketPayload[SocketEvent];
      } else {
        const [first] = args;
        payload = first as SocketPayload[SocketEvent];
      }

      console.log("🔄 [Socket] Received event:", event, payload);

      socketEventReceived({
        type: event,
        payload,
      });
    };

    conn.on(event, handler);
    handlers.set(event, handler);
  });

  const releaseHandlers = () => handlers.forEach((h, ev) => conn.off(ev, h));

  conn.onreconnecting((error) => {
    if (isStale()) return;
    connectionReconnecting(error);
  });

  conn.onreconnected((connectionId) => {
    if (isStale()) return;
    connectionReconnected(connectionId ?? null);
    setSignalRConnectionId(connectionId ?? null);
  });

  conn.onclose((e) => {
    releaseHandlers();
    if (isStale()) return;

    if ($connection.getState() === (conn as ConnectionReady)) {
      connectionClosed(e);
      setSignalRConnectionId(null);
    }
  });

  try {
    await startConnection(conn);
  } catch (e) {
    releaseHandlers();
    // за место в сторе уже борется более свежая попытка — молча уходим
    if (isStale()) return;

    console.error(e);
    connectionClosed(e as Error);
    setSignalRConnectionId(null);
    return;
  }

  // пока подключались, кто-то запросил пересоздание — это соединение лишнее
  if (isStale()) {
    releaseHandlers();
    void waitAtMost(() => conn.stop(), stopTimeoutMs);
    return;
  }

  connectionStarted(Object.assign(conn, { started: true }) as ConnectionReady);
});

/** Пересоздаёт HubConnection, чей сетевой канал умер после сна машины */
export const restartSocketFx = socket.createEffect(async () => {
  const staleConnection = $connection.getState();

  if (staleConnection) {
    // Обесцениваем попытку до stop(): мёртвый транспорт может не ответить.
    socketGeneration++;

    await waitAtMost(() => staleConnection.stop(), stopTimeoutMs);

    // Страховка: оборванный транспорт мог не доставить onclose.
    if ($connection.getState() === staleConnection) {
      connectionClosed(undefined);
      setSignalRConnectionId(null);
    }
  }

  await initSocketFx();
});
