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

/**
 * Номер поколения сокета: растёт при каждой попытке подключиться. Попытка с
 * устаревшим номером никому не нужна — её колбэки игнорируются, а соединение
 * закрывается. Благодаря этому новая попытка никогда не ждёт зависшую старую
 * (в фоновой вкладке setTimeout зажат до 1 раза в минуту, и ожидание может
 * длиться минутами).
 */
let socketGeneration = 0;

/** Ждём промис, но не дольше timeoutMs — мёртвый транспорт может не ответить никогда */
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

/** Сигналы окружения, по которым поднимаем упавшее соединение */

/**
 * Пользователь вернулся к странице: вкладка стала видимой, окно получило
 * фокус или страницу достали из bfcache. Все три случая лечатся одинаково,
 * поэтому различать их незачем.
 */
export const appWokeUp = socket.createEvent();

/** Машина спала: соединение могло остаться Connected, но уже не работает */
export const longSleepDetected = socket.createEvent();
export const browserWentOnline = socket.createEvent();
export const browserWentOffline = socket.createEvent();
export const connectionRecoveryStarted = socket.createEvent();
export const reconnectStuckDetected = socket.createEvent();

/**
 * Связь восстановлена после разрыва (в отличие от первого подключения).
 * Пока связи не было, серверные события уходили в никуда — данные надо перезабрать.
 */
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

/**
 * connectionId текущего соединения. Меняется и при первом старте, и при каждом
 * реконнекте — членство в группах хаба привязано к нему и после смены теряется.
 */
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

/** Пауза перед повторным подъёмом соединения после окончательного закрытия */
export const waitBeforeRevivalFx = socket.createEffect(
  (delayMs: number) =>
    new Promise<void>((resolve) => setTimeout(resolve, delayMs))
);

/** Таймер фоллбэк-баннера; создаётся один раз на цикл восстановления. */
export const waitForReconnectStuckFx = socket.createEffect(
  (recoveryId: number) =>
    new Promise<number>((resolve) =>
      setTimeout(() => resolve(recoveryId), reconnectStuckTimeoutMs)
    )
);

/**
 * Счётчик пауз перед попытками поднять соединение. Растёт, пока сервер
 * недоступен, и обнуляется удачным стартом: после него обрывами занимается
 * retry-политика SignalR, так что до этой цепочки они уже не доходят.
 */
export const $revivalAttempts = socket
  .createStore(0)
  .on(waitBeforeRevivalFx, (count) => count + 1)
  .reset(connectionStarted);

/** Было ли соединение потеряно — чтобы отличить восстановление от первого старта */
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

/** Восстановление длится больше двух минут — показываем безопасный фоллбэк. */
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

  /** Карта handlers, нужна чтобы затем корректно вызвать `conn.off` */
  const handlers = new Map<SocketEvent, (p: unknown) => void>();

  // регистрируем единый набор хендлеров
  Object.values(SocketEvent).forEach((event) => {
    const customParser = customParsers[event];

    const handler = (...args: unknown[]) => {
      if (isStale()) return;

      let payload: SocketPayload[SocketEvent];

      if (customParser) {
        // кастомный парсер знает как распаковать args
        payload = customParser(...args) as SocketPayload[SocketEvent];
      } else {
        // дефолт: берём первый аргумент как payload
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

  // системные события соединения
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
    releaseHandlers(); // clean-up
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

/**
 * Принудительно пересоздаёт застрявший HubConnection. Нужен после сна, когда
 * браузер оставляет объект в памяти, но его сетевой канал уже не жизнеспособен.
 */
export const restartSocketFx = socket.createEffect(async () => {
  const staleConnection = $connection.getState();

  if (staleConnection) {
    // Обесцениваем текущую попытку до stop(): даже если транспорт мёртв и
    // stop() не ответит никогда, её колбэки уже ни на что не влияют.
    socketGeneration++;

    await waitAtMost(() => staleConnection.stop(), stopTimeoutMs);

    // stop() штатно вызывает onclose. Это страховка на случай оборванного
    // транспорта, который не доставил callback.
    if ($connection.getState() === staleConnection) {
      connectionClosed(undefined);
      setSignalRConnectionId(null);
    }
  }

  await initSocketFx();
});
