import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";

/**
 * Тесты гоняют настоящие initSocketFx/restartSocketFx поверх фейкового
 * HubConnection — подмена самих эффектов моками скрыла бы взаимные блокировки
 * между попытками подключения.
 */
type FakeConnection = {
  state: HubConnectionState;
  connectionId: string;
  start: () => Promise<void>;
  stop: () => Promise<void>;
  on: () => void;
  off: () => void;
  onreconnecting: (handler: (error?: Error) => void) => void;
  onreconnected: (handler: (connectionId?: string) => void) => void;
  onclose: (handler: (error?: Error) => void) => void;
  triggerClose: (error?: Error) => void;
};

const connections: FakeConnection[] = [];
let startBehavior: () => Promise<void> = () => Promise.resolve();

const createFakeConnection = (): FakeConnection => {
  let closeHandler: (error?: Error) => void = () => {};

  const connection: FakeConnection = {
    state: HubConnectionState.Disconnected,
    connectionId: `conn-${connections.length + 1}`,
    start: () =>
      startBehavior().then(() => {
        connection.state = HubConnectionState.Connected;
      }),
    stop: () => {
      connection.state = HubConnectionState.Disconnected;
      closeHandler(undefined);
      return Promise.resolve();
    },
    on: () => {},
    off: () => {},
    onreconnecting: () => {},
    onreconnected: () => {},
    onclose: (handler) => {
      closeHandler = handler;
    },
    triggerClose: (error) => {
      connection.state = HubConnectionState.Disconnected;
      closeHandler(error);
    },
  };

  connections.push(connection);
  return connection;
};

vi.mock("./connection", () => ({
  buildConnection: () => createFakeConnection(),
  startConnection: (conn: FakeConnection) => conn.start(),
}));

vi.mock("@/shared/api", () => ({
  setSignalRConnectionId: vi.fn(),
  getAppWebSocketUrl: () => "/api/app/v1/report-page-hub",
}));

import {
  $connection,
  connectionClosed,
  initSocketFx,
  restartSocketFx,
  stopTimeoutMs,
} from "./model";

/** Даёт микротаскам провернуться, не продвигая таймеры */
const flushMicrotasks = () => new Promise((resolve) => setTimeout(resolve, 0));

const hangForever = () => new Promise<void>(() => {});

beforeEach(() => {
  connections.length = 0;
  startBehavior = () => Promise.resolve();
  connectionClosed(undefined); // сбрасываем $connection между тестами
});

afterEach(() => {
  vi.useRealTimers();
});

describe("initSocketFx", () => {
  it("builds a connection and puts it into the store", async () => {
    await initSocketFx();

    expect(connections).toHaveLength(1);
    expect($connection.getState()).toBe(connections[0]);
  });

  it("does not build a second connection while one is alive", async () => {
    await initSocketFx();
    await initSocketFx();

    expect(connections).toHaveLength(1);
  });
});

describe("restartSocketFx", () => {
  /**
   * Регрессия: раньше вторая попытка ждала промис первой. В фоновой вкладке
   * та висела минутами, и возврат пользователя не поднимал соединение.
   */
  it("supersedes a connection attempt that hangs instead of waiting for it", async () => {
    startBehavior = hangForever;
    void initSocketFx();
    await flushMicrotasks();
    expect(connections).toHaveLength(1);
    expect($connection.getState()).toBeNull();

    // пользователь вернулся на вкладку
    startBehavior = () => Promise.resolve();
    await restartSocketFx();

    expect(connections).toHaveLength(2);
    expect($connection.getState()).toBe(connections[1]);
  });

  it("gives up on a zombie socket whose stop never resolves", async () => {
    await initSocketFx();
    const zombie = connections[0];
    zombie.stop = hangForever;

    vi.useFakeTimers();
    const restarting = restartSocketFx();
    await vi.advanceTimersByTimeAsync(stopTimeoutMs + 100);
    await restarting;

    expect(connections).toHaveLength(2);
    expect($connection.getState()).toBe(connections[1]);
  });

  it("keeps the fresh connection when a stale attempt finally settles", async () => {
    let releaseFirstAttempt: () => void = () => {};
    startBehavior = () =>
      new Promise<void>((resolve) => {
        releaseFirstAttempt = resolve;
      });
    void initSocketFx();
    await flushMicrotasks();

    startBehavior = () => Promise.resolve();
    await restartSocketFx();
    const freshConnection = $connection.getState();

    // зависшая попытка завершилась уже после того, как её сменили
    releaseFirstAttempt();
    await flushMicrotasks();

    expect($connection.getState()).toBe(freshConnection);
  });

  it("ignores a close callback from a superseded attempt", async () => {
    await initSocketFx();
    const firstConnection = connections[0];

    await restartSocketFx();
    const freshConnection = $connection.getState();
    expect(freshConnection).toBe(connections[1]);

    // мёртвый транспорт с опозданием доставил onclose
    firstConnection.triggerClose(new Error("transport closed"));

    expect($connection.getState()).toBe(freshConnection);
  });
});
