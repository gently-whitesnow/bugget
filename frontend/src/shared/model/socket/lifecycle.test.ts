// @vitest-environment jsdom
import { describe, expect, it, vi, afterEach } from "vitest";

vi.mock("@/shared/api", () => ({
  setSignalRConnectionId: vi.fn(),
  getAppWebSocketUrl: () => "/api/app/v1/report-page-hub",
}));

import { startSocketLifecycle } from "./lifecycle";
import { longSleepDetected, appWokeUp } from "./model";

const watchdogIntervalMs = 20_000;

let stopLifecycle: (() => void) | null = null;

afterEach(() => {
  stopLifecycle?.();
  stopLifecycle = null;
  vi.useRealTimers();
});

describe("socket lifecycle watchdog", () => {
  it("stays quiet while timers tick on schedule", () => {
    vi.useFakeTimers();
    const longSleep = vi.fn();
    const unwatch = longSleepDetected.watch(longSleep);

    stopLifecycle = startSocketLifecycle();
    vi.advanceTimersByTime(watchdogIntervalMs * 5);

    expect(longSleep).not.toHaveBeenCalled();
    unwatch();
  });

  /**
   * Сон машины с активной вкладкой: visibilitychange и focus не приходят,
   * поэтому единственный признак — таймер, проснувшийся сильно позже срока.
   */
  it("detects a machine sleep from a clock jump", () => {
    vi.useFakeTimers();
    const longSleep = vi.fn();
    const unwatch = longSleepDetected.watch(longSleep);

    stopLifecycle = startSocketLifecycle();

    // часы ушли вперёд на 10 минут, пока таймеры стояли
    vi.setSystemTime(Date.now() + 10 * 60_000);
    vi.advanceTimersByTime(watchdogIntervalMs);

    expect(longSleep).toHaveBeenCalledTimes(1);
    unwatch();
  });

  it("stops watching after cleanup", () => {
    vi.useFakeTimers();
    const longSleep = vi.fn();
    const unwatch = longSleepDetected.watch(longSleep);

    const stop = startSocketLifecycle();
    stop();

    vi.setSystemTime(Date.now() + 10 * 60_000);
    vi.advanceTimersByTime(watchdogIntervalMs);

    expect(longSleep).not.toHaveBeenCalled();
    unwatch();
  });
});

describe("socket lifecycle visibility", () => {
  it("reports a plain wake-up when the tab returns quickly", () => {
    const visible = vi.fn();
    const longSleep = vi.fn();
    const unwatchVisible = appWokeUp.watch(visible);
    const unwatchSleep = longSleepDetected.watch(longSleep);

    stopLifecycle = startSocketLifecycle();
    document.dispatchEvent(new Event("visibilitychange"));

    expect(visible).toHaveBeenCalledTimes(1);
    expect(longSleep).not.toHaveBeenCalled();
    unwatchVisible();
    unwatchSleep();
  });

  it("reports a long sleep instead of a plain wake-up after hours away", () => {
    vi.useFakeTimers();
    const visible = vi.fn();
    const longSleep = vi.fn();
    const unwatchVisible = appWokeUp.watch(visible);
    const unwatchSleep = longSleepDetected.watch(longSleep);

    stopLifecycle = startSocketLifecycle();

    // вкладку спрятали, вернулись через полчаса
    vi.spyOn(document, "visibilityState", "get").mockReturnValue("hidden");
    document.dispatchEvent(new Event("visibilitychange"));

    vi.setSystemTime(Date.now() + 30 * 60_000);
    vi.spyOn(document, "visibilityState", "get").mockReturnValue("visible");
    document.dispatchEvent(new Event("visibilitychange"));

    expect(longSleep).toHaveBeenCalledTimes(1);
    expect(visible).not.toHaveBeenCalled();
    unwatchVisible();
    unwatchSleep();
  });
});
