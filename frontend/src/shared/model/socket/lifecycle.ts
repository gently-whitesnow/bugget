import { scopeBind } from "effector";

import {
  appWokeUp,
  browserWentOffline,
  browserWentOnline,
  longSleepDetected,
} from "./model";

const longSleepThresholdMs = 5 * 60_000;
const watchdogIntervalMs = 20_000;

/**
 * Насколько тик сторожа должен опоздать, чтобы счесть это сном машины.
 * В фоновой вкладке браузер и так растягивает интервал примерно до минуты,
 * поэтому берём заметно больший порог, чем это опоздание.
 */
const timeJumpThresholdMs = 90_000;

/**
 * Подписка на сигналы окружения, влияющие на живучесть сокета.
 * Возвращает функцию отписки.
 */
export const startSocketLifecycle = (): (() => void) => {
  const notifyWokeUp = scopeBind(appWokeUp, { safe: true });
  const notifyLongSleep = scopeBind(longSleepDetected, { safe: true });
  const notifyOnline = scopeBind(browserWentOnline, { safe: true });
  const notifyOffline = scopeBind(browserWentOffline, { safe: true });
  let lastLifecycleActivityAt = Date.now();

  const notifyWake = () => {
    const currentActivityAt = Date.now();
    const wasLongSleep =
      currentActivityAt - lastLifecycleActivityAt >= longSleepThresholdMs;

    lastLifecycleActivityAt = currentActivityAt;

    // Долгий сон и так ведёт к принудительному пересозданию соединения,
    // поэтому обычный сигнал пробуждения в этом случае не нужен.
    if (wasLongSleep) {
      notifyLongSleep();
      return;
    }

    notifyWokeUp();
  };

  const handleVisibilityChange = () => {
    if (document.visibilityState === "visible") {
      notifyWake();
      return;
    }

    lastLifecycleActivityAt = Date.now();
  };
  const handleFocus = () => notifyWake();
  const handlePageShow = () => notifyWake();
  const handleOnline = () => notifyOnline();
  const handleOffline = () => notifyOffline();

  /**
   * Сторож на случай сна машины с активной вкладкой: ни visibilitychange, ни
   * focus тогда не приходят, и без него засыпание осталось бы незамеченным.
   * Ловим по опозданию тика — часы прыгают вперёд, пока таймеры стоят.
   */
  let expectedTickAt = Date.now() + watchdogIntervalMs;
  const watchdogId = setInterval(() => {
    const currentTickAt = Date.now();
    const tickDelay = currentTickAt - expectedTickAt;
    expectedTickAt = currentTickAt + watchdogIntervalMs;

    if (tickDelay >= timeJumpThresholdMs) {
      lastLifecycleActivityAt = currentTickAt;
      notifyLongSleep();
    }
  }, watchdogIntervalMs);

  document.addEventListener("visibilitychange", handleVisibilityChange);
  window.addEventListener("focus", handleFocus);
  window.addEventListener("pageshow", handlePageShow);
  window.addEventListener("online", handleOnline);
  window.addEventListener("offline", handleOffline);

  return () => {
    clearInterval(watchdogId);
    document.removeEventListener("visibilitychange", handleVisibilityChange);
    window.removeEventListener("focus", handleFocus);
    window.removeEventListener("pageshow", handlePageShow);
    window.removeEventListener("online", handleOnline);
    window.removeEventListener("offline", handleOffline);
  };
};
