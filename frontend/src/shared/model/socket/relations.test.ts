import { describe, expect, it, vi } from "vitest";
import { allSettled, fork, type Scope } from "effector";
import { HubConnectionState } from "@microsoft/signalr";

import "./relations";
import {
  browserWentOffline,
  browserWentOnline,
  connectionClosed,
  connectionReconnected,
  connectionReconnecting,
  connectionRestored,
  connectionStarted,
  initSocketFx,
  longSleepDetected,
  restartSocketFx,
  appWokeUp,
  waitForReconnectStuckFx,
  waitBeforeRevivalFx,
  $connectionId,
  $reconnectStuck,
  $socketConnectionStatus,
  SocketConnectionStatus,
} from "./model";

vi.mock("@/shared/api", () => ({
  setSignalRConnectionId: vi.fn(),
  getAppWebSocketUrl: () => "/api/app/v1/report-page-hub",
}));

type FakeConnection = Parameters<typeof connectionStarted>[0];

const fakeConnection = (connectionId = "conn-1") =>
  ({
    connectionId,
    state: HubConnectionState.Connected,
    started: true,
  }) as unknown as FakeConnection;

/**
 * Реальные эффекты заменяем моками: ждать 30s бэк-оффа и поднимать настоящий
 * SignalR в тестах нечем. Триггеры при этом срабатывают как в проде.
 */
const setup = () => {
  const initCalls: unknown[] = [];
  const restartCalls: unknown[] = [];
  const revivalDelays: number[] = [];

  const scope = fork({
    handlers: [
      [initSocketFx, () => initCalls.push(Date.now())],
      [restartSocketFx, () => restartCalls.push(Date.now())],
      [
        waitBeforeRevivalFx,
        (delay: number) => {
          revivalDelays.push(delay);
          return Promise.resolve();
        },
      ],
      [
        waitForReconnectStuckFx,
        (recoveryId: Parameters<typeof waitForReconnectStuckFx>[0]) =>
          Promise.resolve(recoveryId),
      ],
    ],
  });

  return { scope, initCalls, restartCalls, revivalDelays };
};

const close = (scope: Scope) =>
  allSettled(connectionClosed, { scope, params: undefined });

describe("$connectionId", () => {
  it("tracks the id across start, reconnect and close", async () => {
    const { scope } = setup();

    await allSettled(connectionStarted, {
      scope,
      params: fakeConnection("first"),
    });
    expect(scope.getState($connectionId)).toBe("first");

    // реконнект: объект соединения тот же, id новый — группу надо перезанять
    await allSettled(connectionReconnected, { scope, params: "second" });
    expect(scope.getState($connectionId)).toBe("second");

    await close(scope);
    expect(scope.getState($connectionId)).toBeNull();
  });
});

describe("$reconnectStuck", () => {
  it("turns on after a recovery timeout and off once connected", async () => {
    const { scope } = setup();

    await close(scope);
    expect(scope.getState($reconnectStuck)).toBe(true);

    await allSettled(connectionStarted, { scope, params: fakeConnection() });
    expect(scope.getState($reconnectStuck)).toBe(false);
  });
});

describe("socket revival", () => {
  it("restarts the connection after it closes for good", async () => {
    const { scope, initCalls } = setup();

    await close(scope);

    expect(initCalls).toHaveLength(1);
  });

  it("does not restart while the browser is offline", async () => {
    const { scope, initCalls } = setup();

    await allSettled(browserWentOffline, { scope, params: undefined });
    await close(scope);

    expect(initCalls).toHaveLength(0);
  });

  it("restarts as soon as the network comes back", async () => {
    const { scope, restartCalls } = setup();

    await allSettled(browserWentOffline, { scope, params: undefined });
    await close(scope);
    expect(restartCalls).toHaveLength(0);

    await allSettled(browserWentOnline, { scope, params: undefined });

    expect(restartCalls).toHaveLength(1);
  });

  it("restarts immediately when the user comes back to the tab", async () => {
    const { scope, restartCalls } = setup();

    await allSettled(appWokeUp, { scope, params: undefined });

    expect(restartCalls).toHaveLength(1);
  });

  it("does nothing on tab focus while the connection is alive", async () => {
    const { scope, restartCalls } = setup();

    await allSettled(connectionStarted, { scope, params: fakeConnection() });
    await allSettled(appWokeUp, { scope, params: undefined });

    expect(restartCalls).toHaveLength(0);
  });

  it("restarts a SignalR connection stuck in Reconnecting after tab return", async () => {
    const { scope, restartCalls } = setup();

    await allSettled(connectionStarted, { scope, params: fakeConnection() });
    await allSettled(connectionReconnecting, { scope, params: undefined });
    expect(scope.getState($socketConnectionStatus)).toBe(
      SocketConnectionStatus.DISCONNECTED
    );

    await allSettled(appWokeUp, { scope, params: undefined });

    expect(restartCalls).toHaveLength(1);
  });

  it("restarts a stale Connected connection after a long sleep", async () => {
    const { scope, restartCalls } = setup();

    await allSettled(connectionStarted, { scope, params: fakeConnection() });
    await allSettled(longSleepDetected, { scope, params: undefined });

    expect(restartCalls).toHaveLength(1);
  });

  it("backs off when the connection keeps dropping", async () => {
    const { scope, revivalDelays } = setup();

    await close(scope);
    await close(scope);
    await close(scope);

    // первая попытка — сразу, дальше растущая пауза (±20% jitter)
    expect(revivalDelays[0]).toBe(0);
    expect(revivalDelays[1]).toBeGreaterThanOrEqual(800);
    expect(revivalDelays[1]).toBeLessThanOrEqual(1_200);
    expect(revivalDelays[2]).toBeGreaterThanOrEqual(1_600);
    expect(revivalDelays[2]).toBeLessThanOrEqual(2_400);
  });

  it("does not report a restore on the very first connection", async () => {
    const { scope } = setup();
    const restored = vi.fn();
    const unwatch = connectionRestored.watch(restored);

    await allSettled(connectionStarted, { scope, params: fakeConnection() });

    expect(restored).not.toHaveBeenCalled();
    unwatch();
  });

  it("reports a restore when the connection comes back after a drop", async () => {
    const { scope } = setup();
    const restored = vi.fn();
    const unwatch = connectionRestored.watch(restored);

    await allSettled(connectionStarted, { scope, params: fakeConnection() });
    await close(scope);
    await allSettled(connectionStarted, { scope, params: fakeConnection("2") });

    expect(restored).toHaveBeenCalledTimes(1);
    unwatch();
  });

  it("resets the backoff once the connection is up again", async () => {
    const { scope, revivalDelays } = setup();

    await close(scope);
    await close(scope);
    expect(revivalDelays[1]).toBeGreaterThan(0);

    await allSettled(connectionStarted, { scope, params: fakeConnection() });
    await close(scope);

    expect(revivalDelays[2]).toBe(0);
  });
});
